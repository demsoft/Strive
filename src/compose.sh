export GITCOMMIT=$(git rev-parse --short HEAD)
export GITREF="$(git log -1 --pretty=format:"%D")"
export GITTIMESTAMP="$(git log -1 --pretty=format:"%ai")"

export COMMIT_INFO_BRANCH=$(git rev-parse --abbrev-ref HEAD)
export COMMIT_INFO_MESSAGE=$(git show -s --pretty=%B)
export COMMIT_INFO_EMAIL=$(git show -s --pretty=%ae)
export COMMIT_INFO_AUTHOR=$(git show -s --pretty=%an)
export COMMIT_INFO_SHA=$(git show -s --pretty=%H)
export COMMIT_INFO_REMOTE=$(git config --get remote.origin.url)

# Traefik serves a persistent self-signed certificate for localhost in development. Without it, Traefik generates a
# new one whenever its container is recreated and browsers reject the old exception ("WebRTC connection error").
if [ ! -f certs/localhost.crt ] || [ ! -f certs/localhost.key ]; then
  echo "Creating the development certificate certs/localhost.crt"
  mkdir -p certs
  openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -keyout certs/localhost.key -out certs/localhost.crt \
    -subj "/CN=localhost" -addext "subjectAltName=DNS:localhost,DNS:*.localhost,IP:127.0.0.1" 2>/dev/null
fi

# Recording is optional. It runs only with `--profile recording`, which also switches the feature on in the server.
# Recordings are stored in Cloudflare R2 when the file .env.recording (never committed) defines
#   R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY and R2_BUCKET.
# Without that file a small local S3-compatible storage is started instead (profile local-storage), so that recording
# can be tried without a cloud account. Set RECORDING_STORAGE=local to use it even if the R2 credentials exist.
case " $* " in
  *" --profile recording "*)
    export RECORDING_ENABLED=true

    use_r2=false
    if [ -f .env.recording ] && [ "${RECORDING_STORAGE:-}" != "local" ]; then
      set -a
      . ./.env.recording
      set +a
      for variable in R2_ACCOUNT_ID R2_ACCESS_KEY_ID R2_SECRET_ACCESS_KEY R2_BUCKET; do
        if [ -z "$(eval echo \"\${$variable:-}\")" ]; then
          echo ".env.recording does not define $variable" >&2
          exit 1
        fi
      done
      use_r2=true
    fi

    if [ "$use_r2" = true ]; then
      export RECORDING_STORAGE_ENDPOINT="https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com"
      export RECORDING_STORAGE_BUCKET="$R2_BUCKET"
      export RECORDING_STORAGE_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID"
      export RECORDING_STORAGE_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY"
      echo "Recording storage: Cloudflare R2 (bucket $R2_BUCKET)"
    else
      export RECORDING_STORAGE_ENDPOINT="http://storage:9000"
      # the recorder (development: on the docker host) reaches it directly, browsers through the reverse proxy (https)
      export RECORDING_STORAGE_DEV_ENDPOINT="http://127.0.0.1:9100"
      site_host=$(grep '^SITE_HOST=' .env | cut -d= -f2)
      export RECORDING_STORAGE_PUBLIC_URL="https://storage.${site_host:-localhost}"
      export RECORDING_STORAGE_BUCKET="${RECORDING_STORAGE_BUCKET:-strive-recordings}"
      export RECORDING_STORAGE_ACCESS_KEY_ID="${LOCAL_STORAGE_ACCESS_KEY:-strive}"
      export RECORDING_STORAGE_SECRET_ACCESS_KEY="${LOCAL_STORAGE_SECRET_KEY:-strive-dev-storage-secret}"
      export RECORDING_STORAGE_FORCE_PATH_STYLE=true
      # the region the local gateway signs for (R2 uses "auto")
      export RECORDING_STORAGE_REGION=us-east-1
      set -- --profile local-storage "$@"
      echo "Recording storage: local S3-compatible storage"
    fi
    ;;
esac

echo "GITREF=$GITREF"
echo "GITCOMMIT=$GITCOMMIT"
echo "GITTIMESTAMP=$GITTIMESTAMP"

# Docker Compose v2 is a docker plugin ("docker compose"), fall back to the standalone binary
if docker compose version >/dev/null 2>&1; then compose="docker compose"; else compose="docker-compose"; fi

$compose -f docker-compose.yml -f docker-compose.override.yml -f docker-compose.dev.yml -f docker-compose.traefik.yml "$@"