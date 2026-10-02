export GITCOMMIT=$(git rev-parse --short HEAD)
export GITREF="$(git log -1 --pretty=format:"%D")"
export GITTIMESTAMP="$(git log -1 --pretty=format:"%ai")"

export COMMIT_INFO_BRANCH=$(git rev-parse --abbrev-ref HEAD)
export COMMIT_INFO_MESSAGE=$(git show -s --pretty=%B)
export COMMIT_INFO_EMAIL=$(git show -s --pretty=%ae)
export COMMIT_INFO_AUTHOR=$(git show -s --pretty=%an)
export COMMIT_INFO_SHA=$(git show -s --pretty=%H)
export COMMIT_INFO_REMOTE=$(git config --get remote.origin.url)

echo "GITREF=$GITREF"
echo "GITCOMMIT=$GITCOMMIT"
echo "GITTIMESTAMP=$GITTIMESTAMP"

# Docker Compose v2 is a docker plugin ("docker compose"), fall back to the standalone binary
if docker compose version >/dev/null 2>&1; then compose="docker compose"; else compose="docker-compose"; fi

$compose -f docker-compose.yml -f docker-compose.override.yml -f docker-compose.dev.yml -f docker-compose.traefik.yml "$@"