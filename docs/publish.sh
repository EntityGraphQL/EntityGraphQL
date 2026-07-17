#!/bin/bash

# Builds the docs site (current + versioned snapshots - see docusaurus.config.js for versioning)
# and pushes it to the entitygraphql.github.io repo.
# Optional argument: a label for the publish commit message, e.g. ./publish.sh 6.0.0-beta9

MSG=${1:-$(date +%Y-%m-%d)}

echo Building documentation site
npm run build || exit 1

echo Publishing
npx gh-pages -d build -b main -r git@github.com:entitygraphql/entitygraphql.github.io.git -m "Documentation update: $MSG"
