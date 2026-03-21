#!/usr/bin/env bash
# Usage:
#   RENDER_API_KEY=rnd_xxx SERVICE_ID=srv_xxx ./scripts/render-debug.sh <command>
#
# Commands:
#   services          List all services in the account
#   status            Show latest deploy status for SERVICE_ID
#   logs              Tail the last 100 lines of logs for SERVICE_ID
#   deploys           List recent deployments
#   deploy            Trigger a new deploy
#   env               List environment variables (values redacted by Render)

BASE="https://api.render.com/v1"
AUTH="Authorization: Bearer ${RENDER_API_KEY}"

render_services() {
  curl -sf -H "$AUTH" "$BASE/services?limit=20" | jq '.[] | {id: .service.id, name: .service.name, status: .service.suspended}'
}
render_status() {
  curl -sf -H "$AUTH" "$BASE/services/$SERVICE_ID/deploys?limit=1" | jq '.[0]'
}
render_logs() {
  curl -sf -H "$AUTH" "$BASE/services/$SERVICE_ID/logs?limit=100" | jq -r '.[] | "\(.timestamp) \(.message)"'
}
render_deploys() {
  curl -sf -H "$AUTH" "$BASE/services/$SERVICE_ID/deploys?limit=5" | jq '.[] | {id: .deploy.id, status: .deploy.status, createdAt: .deploy.createdAt}'
}
render_deploy() {
  curl -sf -X POST -H "$AUTH" -H "Content-Type: application/json" \
    "$BASE/services/$SERVICE_ID/deploys" | jq '.'
}
render_env() {
  curl -sf -H "$AUTH" "$BASE/services/$SERVICE_ID/env-vars" | jq '.[] | {key: .envVar.key}'
}

case "$1" in
  services) render_services ;;
  status)   render_status ;;
  logs)     render_logs ;;
  deploys)  render_deploys ;;
  deploy)   render_deploy ;;
  env)      render_env ;;
  *)
    echo "Usage: RENDER_API_KEY=... SERVICE_ID=... $0 <services|status|logs|deploys|deploy|env>"
    exit 1
    ;;
esac
