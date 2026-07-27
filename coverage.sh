#!/bin/sh
# Test coverage for the shipped packages only. See coverage.runsettings.
set -e
rm -rf coverage
dotnet test EntityGraphQL.sln --collect:"Code Coverage" -s coverage.runsettings "$@"
mv coverage/*/*.cobertura.xml coverage/coverage.cobertura.xml
grep -o '<coverage [^>]*>\|<package [^>]*>' coverage/coverage.cobertura.xml | awk '
function pct(tag, s) { match(s, tag "-rate=\"[^\"]*\""); return substr(s, RSTART + length(tag) + 7, RLENGTH - length(tag) - 8) * 100 }
BEGIN { printf "\n%-34s %7s %9s\n", "Package", "Lines", "Branches" }
{
  if (/^<coverage/) { total = sprintf("%-34s %6.2f%% %8.2f%%", "TOTAL (shipped packages)", pct("line", $0), pct("branch", $0)); next }
  match($0, /name="[^"]*"/)
  printf "%-34s %6.2f%% %8.2f%%\n", substr($0, RSTART + 6, RLENGTH - 7), pct("line", $0), pct("branch", $0)
}
END { print total }
'

