#!/usr/bin/env bash
# Run after deploying LKHK V1.9.4.4: bash tools/seo-smoke.sh
set -euo pipefail
SITE="${LKH_PUBLIC_ORIGIN:-https://langkinhhukhong.duckdns.org}"
SITE="${SITE%/}"
for path in / /thu-vien /robots.txt /sitemap.xml /sitemap-static.xml /favicon.svg /images/lkh-logo.svg /images/lkh-social.png; do
  printf '%-42s ' "$path"
  curl -sS -o /dev/null -w '%{http_code} %{content_type}\n' "$SITE$path"
done
printf '\nHome SEO tags (first occurrences):\n'
curl -fsS "$SITE/" | grep -Eo 'name="description"|rel="canonical"|application/ld\+json|Truyện audio mới cập nhật' | sort -u || true
printf '\nXML syntax checks:\n'
for path in sitemap.xml sitemap-static.xml; do
  curl -fsS "$SITE/$path" | python3 -c 'import sys,xml.etree.ElementTree as ET; root=ET.parse(sys.stdin).getroot();print(root.tag)'
done
