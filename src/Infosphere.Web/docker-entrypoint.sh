#!/bin/sh
set -eu

envsubst '${API_BASE_URL}' < /opt/config.template.js > /usr/share/nginx/html/config.js
