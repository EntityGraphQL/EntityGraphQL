#!/bin/bash

if [ -z "$1" ]
then

  echo
  echo ERROR: Please provide a version
  echo

else

  echo Generating documentation for Version $1

  npx gatsby build

  echo Publishing

  npx gh-pages -d public -b main -r git@github.com:entitygraphql/entitygraphql.github.io.git -m "Documentation update for $1"

fi