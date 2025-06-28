#!/bin/bash

set -euo pipefail

NAMESPACE="redis"
RELEASE_NAME="redis"
CHART_REPO="https://charts.bitnami.com/bitnami"
CHART_NAME="bitnami/redis"
HOST_PATH="/mnt/redis-data"
NODE_NAME="sch-g-04"
PV_NAME="redis-pv"
PVC_NAME="redis-pvc"

# Check if Redis is already installed and get existing password, or generate new one
if kubectl get secret -n $NAMESPACE ${RELEASE_NAME} >/dev/null 2>&1; then
  echo "🔍 Redis already installed, retrieving existing password..."
  REDIS_PASSWORD=$(kubectl get secret -n $NAMESPACE ${RELEASE_NAME} -o jsonpath='{.data.redis-password}' | base64 -d)
  echo "🔑 Using existing Redis password $REDIS_PASSWORD"
else
  echo "🆕 Redis not found, generating new password..."
  REDIS_PASSWORD=$(openssl rand -base64 16)
  echo "🔑 Generated new Redis password"
fi

# Ensure the hostPath directory exists and has the correct permissions
if [ ! -d "$HOST_PATH" ]; then
  sudo mkdir -p "$HOST_PATH"
  sudo chown 1000:1000 "$HOST_PATH"
  sudo chmod 700 "$HOST_PATH"
fi

sudo apt install redis-tools -y

echo "🚀 Checking required tools..."
command -v kubectl >/dev/null || { echo "kubectl not found"; exit 1; }
command -v helm >/dev/null || { echo "helm not found"; exit 1; }

echo "🔧 Creating namespace if not exists: $NAMESPACE"
kubectl get ns $NAMESPACE >/dev/null 2>&1 || kubectl create ns $NAMESPACE

echo "📁 Creating hostPath PV bound to node $NODE_NAME..."
kubectl apply -f - <<EOF
apiVersion: v1
kind: PersistentVolume
metadata:
  name: $PV_NAME
spec:
  capacity:
    storage: 1Gi
  accessModes:
    - ReadWriteOnce
  persistentVolumeReclaimPolicy: Retain
  nodeAffinity:
    required:
      nodeSelectorTerms:
        - matchExpressions:
            - key: kubernetes.io/hostname
              operator: In
              values:
                - $NODE_NAME
  hostPath:
    path: "$HOST_PATH"
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $PVC_NAME
  namespace: $NAMESPACE
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
  volumeName: $PV_NAME
EOF

echo "📦 Adding Bitnami Helm repo..."
helm repo add bitnami $CHART_REPO || true
helm repo update

echo "️  Installing/Upgrading Redis with persistence, nodeSelector, and securityContext (UID/GID 1000)..."
helm upgrade --install $RELEASE_NAME $CHART_NAME \
  --namespace $NAMESPACE \
  --set auth.password=$REDIS_PASSWORD \
  --set architecture=standalone \
  --set master.persistence.enabled=true \
  --set master.persistence.existingClaim=$PVC_NAME \
  --set nodeSelector."kubernetes\.io/hostname"=$NODE_NAME \
  --set master.containerSecurityContext.runAsUser=1000 \
  --set master.containerSecurityContext.runAsGroup=1000 \
  --set master.podSecurityContext.fsGroup=1000 \
  --set master.resources.requests.cpu=100m \
  --set master.resources.requests.memory=128Mi \
  --set master.resources.limits.cpu=500m \
  --set master.resources.limits.memory=512Mi \
  --set replica.resources.requests.cpu=0 \
  --set replica.resources.requests.memory=0 \
  --set replica.resources.limits.cpu=0 \
  --set replica.resources.limits.memory=0 \
  --wait

echo "✅ Redis installed and running as UID 1000 / GID 1000 on node '$NODE_NAME'"

echo "📡 Port-forwarding to test connectivity..."
kubectl -n $NAMESPACE port-forward svc/${RELEASE_NAME}-master 6379:6379 &
PF_PID=$!
sleep 5

echo "🔍 Testing connection..."
if redis-cli -a "$REDIS_PASSWORD" ping | grep -q PONG; then
  echo "✅ Redis is up and responding to PING."
else
  echo "❌ Redis test failed."
  kill $PF_PID
  exit 1
fi

kill $PF_PID
