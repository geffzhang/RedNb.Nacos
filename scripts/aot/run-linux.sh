#!/usr/bin/env bash
set -euo pipefail
# Run inside the toolchain container, with the repository at /source (read-only),
# an output mount at /results and a user-defined network shared with Nacos.
mkdir -p /work
cd /source
git -c safe.directory=/source ls-files -co --exclude-standard -z |
  while IFS= read -r -d '' file; do
    if [ -f "$file" ]; then printf '%s\0' "$file"; fi
  done | tar --null -T - -cf - | tar -C /work -xf -
cd /work
if [ -f /results/nuget-seed.tar ]; then
  mkdir -p /root/.nuget/packages
  tar -C /root/.nuget/packages -xf /results/nuget-seed.tar
  find /root/.nuget/packages/grpc.tools -path '*/linux_x64/*' -type f -exec chmod +x {} +
fi
for framework in net10.0 net8.0; do
  for app in Aot AotWeb; do
    project="samples/RedNb.Nacos.Sample.$app/RedNb.Nacos.Sample.$app.csproj"
    output="/results/linux-x64/$framework/$app"
    mkdir -p "$output"
    dotnet publish "$project" -f "$framework" -p:TargetFrameworks="$framework" -c Release -r linux-x64 --self-contained -o "$output" \
      -p:TrimmerSingleWarn=false -p:ILLinkTreatWarningsAsErrors=true -p:IlcTreatWarningsAsErrors=true \
      2>&1 | tee "$output/publish.log"
    if grep -E 'warning IL[0-9]+' "$output/publish.log"; then
      echo 'Unresolved AOT/trim warning'; exit 1
    fi
    args=()
    if [ "$app" = Aot ]; then args=(--live); fi
    "$output/RedNb.Nacos.Sample.$app" "${args[@]}" 2>&1 | tee "$output/run.log"
    grep -q 'MODE dynamic=False reflection=False' "$output/run.log"
    if [ "$app" = Aot ]; then grep -q 'PASS live SDK contracts' "$output/run.log";
    else grep -q 'PASS native-web' "$output/run.log"; fi
  done
done
bash /source/scripts/aot/run-packages-linux.sh
