#!/bin/bash
set -euo pipefail

# Config
CERT_NAME="aspnet-dev"
PFX_NAME="cert.pfx"
SECRET_NAME="csharp-cert"
PASSWORD_SECRET_NAME="csharp-cert-password"
NAMESPACE="biatec"

# Step 1: Check if password secret exists
if kubectl get secret "$PASSWORD_SECRET_NAME" -n "$NAMESPACE" >/dev/null 2>&1; then
  echo "🔐 Reusing existing password from Kubernetes secret '$PASSWORD_SECRET_NAME'"
  PASSWORD=$(kubectl get secret "$PASSWORD_SECRET_NAME" -n "$NAMESPACE" -o jsonpath='{.data.password}' | base64 -d)
else
  echo "🔐 Generating new secure password"
  PASSWORD=$(openssl rand -base64 32)
  
  # Store password in K8s as a secret
  kubectl create secret generic "$PASSWORD_SECRET_NAME" \
    --from-literal=password="$PASSWORD" \
    --namespace="$NAMESPACE"
  echo "✅ Password stored in Kubernetes secret '$PASSWORD_SECRET_NAME'"
fi

# Step 2: Generate certificate
openssl genrsa -out "${CERT_NAME}.key" 2048
openssl req -new -key "${CERT_NAME}.key" -out "${CERT_NAME}.csr" -subj "/CN=google-account-main-app-deployment.biatec"
openssl x509 -req -days 365 -in "${CERT_NAME}.csr" -signkey "${CERT_NAME}.key" -out "${CERT_NAME}.crt"

# Step 3: Create PFX file
openssl pkcs12 -export \
  -out "${PFX_NAME}" \
  -inkey "${CERT_NAME}.key" \
  -in "${CERT_NAME}.crt" \
  -passout pass:"$PASSWORD"

# Step 4: Create PFX Kubernetes secret
kubectl create secret generic "${SECRET_NAME}" \
  --from-file="${PFX_NAME}" \
  --namespace="${NAMESPACE}" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "✅ Certificate secret '${SECRET_NAME}' created/updated in namespace '${NAMESPACE}'"

# Step 5: Cleanup
rm -f "${CERT_NAME}.key" "${CERT_NAME}.csr" "${CERT_NAME}.crt" "${PFX_NAME}"

echo "🧹 Temporary files cleaned up"