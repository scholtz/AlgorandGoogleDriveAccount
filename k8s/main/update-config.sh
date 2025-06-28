kubectl apply -f deployment-main.yaml -n biatec
kubectl delete configmap google-account-main-conf -n biatec
kubectl create configmap google-account-main-conf --from-file=conf -n biatec
kubectl rollout restart deployment/google-account-main-app-deployment -n biatec
kubectl rollout status deployment/google-account-main-app-deployment -n biatec
