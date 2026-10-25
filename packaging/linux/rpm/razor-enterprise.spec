Name:           razorzip-harness
Version:        0.1.0
Release:        1%{?dist}
Summary:        RazorZip open benchmark harness (Core features)
License:        MIT
URL:            https://github.com/mobisec-cloud/razor
Source0:        %{name}-%{version}.tar.gz

# Commercial engine binaries (razor-bench, razor-watch, razor-train,
# sfx-decode + .integrity sidecars) are packaged ONLY when the
# operator-approved allow-list authorizes them (see scripts/build-installers.sh,
# %%{commercial} conditional below). Default build: harness only, no engine.

BuildArch:      x86_64

Description:
The open-source benchmark GUI + CLI helpers for the Razor structural
compression engine. No commercial binaries, keys, or credentials included.
Trial requests open the user's own email app, prefilled; nothing is sent
automatically.

%description

%prep

%build
# Payload is staged prebuilt by scripts/build-installers.sh (dotnet publish).
# No network fetches here.

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}/usr/lib/razorzip/harness
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/doc/%{name}
cp -a %{_sourcedir}/payload/harness/. %{buildroot}/usr/lib/razorzip/harness/
ln -sf /usr/lib/razorzip/harness/RazorBenchmark %{buildroot}/usr/bin/razor-benchmark
cp -a %{_sourcedir}/payload/razorzip-trial.desktop %{buildroot}/usr/share/applications/
cp -a %{_sourcedir}/payload/trial-request-template.txt %{buildroot}/usr/share/doc/%{name}/

%if %{defined commercial}
# Allow-listed commercial payload ONLY (verified pre-%%install by the build script).
mkdir -p %{buildroot}/usr/lib/razorzip/engine
cp -a %{_sourcedir}/payload/engine/. %{buildroot}/usr/lib/razorzip/engine/
mkdir -p %{buildroot}/usr/lib/systemd/system
cp -a %{_sourcedir}/payload/razor-watch.service %{buildroot}/usr/lib/systemd/system/
%endif

%files
/usr/lib/razorzip/harness/
/usr/bin/razor-benchmark
/usr/share/applications/razorzip-trial.desktop
/usr/share/doc/%{name}/
%if %{defined commercial}
/usr/lib/razorzip/engine/
# Unit file is SHIPPED but NOT enabled/started by %%post (admin opts in).
/usr/lib/systemd/system/razor-watch.service
%endif

%post
# No email, no licensing calls, no service starts. Desktop database only.
update-desktop-database /usr/share/applications >/dev/null 2>&1 || :

%postun
update-desktop-database /usr/share/applications >/dev/null 2>&1 || :
# Never remove user data, corpus caches, or license state here.

%changelog
* Sun Oct 25 2026 Mobisec Cloud <razor@mobisec.cloud> - 0.1.0-1
- Initial public packaging (harness-only; commercial payload allow-list gated)
