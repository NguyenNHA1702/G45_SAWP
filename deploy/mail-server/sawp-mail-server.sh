#!/usr/bin/env bash
#
# sawp-mail-server.sh - install and manage a send-only mail server for SAWP.
#
# Postfix (SMTP, port 587 with STARTTLS + login) + OpenDKIM (signing) + Dovecot (login check only)
# + Let's Encrypt certificate. Run as root on Ubuntu 18.04-24.04 or RHEL/AlmaLinux 8-9 (Dovecot 2.2-2.4).
# Manual steps and background: docs/EMAIL_SERVER_SETUP.md
#
# Usage:
#   sudo ./sawp-mail-server.sh                 interactive menu (or install on first run)
#   sudo ./sawp-mail-server.sh install
#   sudo ./sawp-mail-server.sh add-user <address>
#   sudo ./sawp-mail-server.sh passwd <address>
#   sudo ./sawp-mail-server.sh del-user <address>
#   sudo ./sawp-mail-server.sh list-users
#   sudo ./sawp-mail-server.sh dns              print the DNS records and check them
#   sudo ./sawp-mail-server.sh check            check services, ports, certificate, DKIM, relay
#   sudo ./sawp-mail-server.sh test-send <to> [login]   send a test email through port 587
#   sudo ./sawp-mail-server.sh uninstall
#
# install reads these environment variables as answers (prompts are skipped when set):
#   MAIL_DOMAIN, MAIL_HOST, PUBLIC_IP, DKIM_SELECTOR, SMTP_USER, HAS_OTHER_MX (y/n),
#   CERT_METHOD (standalone|nginx|webroot), CERT_WEBROOT, CERT_EMAIL

set -euo pipefail

CONF_DIR=/etc/sawp-mail
CONF_FILE=$CONF_DIR/config
USERS_FILE=/etc/dovecot/users
DKIM_DIR=/etc/opendkim/keys
DKIM_PORT=8891
RENEW_HOOK=/etc/letsencrypt/renewal-hooks/deploy/sawp-mail-reload-postfix.sh

# ---------------------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------------------

info() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m OK\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m !!\033[0m %s\n' "$*" >&2; }
fail() { printf '\033[1;31mERR\033[0m %s\n' "$*" >&2; }
die()  { fail "$*"; exit 1; }

# ask VAR "Question" "default" - keeps $VAR if it is already set (non-interactive use).
ask() {
    local var=$1 question=$2 default=${3:-} answer
    if [[ -n ${!var:-} ]]; then return; fi
    read -rp "$question${default:+ [$default]}: " answer
    printf -v "$var" '%s' "${answer:-$default}"
}

confirm() {
    local answer
    read -rp "$1 [y/N]: " answer
    [[ $answer =~ ^[Yy]$ ]]
}

require_root() {
    [[ $EUID -eq 0 ]] || die "Run this script as root (sudo $0 ...)."
}

require_installed() {
    [[ -f $CONF_FILE ]] || die "Mail server is not installed yet. Run: $0 install"
    # shellcheck source=/dev/null
    source "$CONF_FILE"
}

valid_domain() {
    [[ $1 =~ ^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$ ]]
}

valid_address() {
    [[ $1 =~ ^[A-Za-z0-9._%+-]+@([A-Za-z0-9-]+\.)+[A-Za-z]{2,}$ ]]
}

random_password() {
    openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 24
}

# DKIM public key as one TXT value, joined from the quoted parts of <selector>.txt.
dkim_txt_value() {
    grep -o '"[^"]*"' "$DKIM_DIR/$MAIL_DOMAIN/$DKIM_SELECTOR.txt" | tr -d '"\n'
}

# ---------------------------------------------------------------------------------------------------
# Operating system: Debian family (apt) or RHEL family (dnf)
# ---------------------------------------------------------------------------------------------------

OS_FAMILY="" OS_ID="" OS_VERSION="" OS_MAJOR="" OS_NAME=""

detect_os() {
    [[ -r /etc/os-release ]] || die "Cannot detect the OS (/etc/os-release is missing)."
    local ID="" ID_LIKE="" VERSION_ID="" PRETTY_NAME=""
    # shellcheck source=/dev/null
    source /etc/os-release
    OS_ID=$ID OS_VERSION=$VERSION_ID OS_MAJOR=${VERSION_ID%%.*} OS_NAME=${PRETTY_NAME:-$ID}
    case " $ID $ID_LIKE " in
        *" debian "*|*" ubuntu "*)         OS_FAMILY=debian ;;
        *" rhel "*|*" centos "*|*" fedora "*) OS_FAMILY=rhel ;;
        *) die "Unsupported OS: $OS_NAME. Supported: Ubuntu 18.04-24.04, RHEL/AlmaLinux 8-9." ;;
    esac
}

# The versions this script was written for; others may work but ask for confirmation.
os_is_supported() {
    case $OS_ID:$OS_VERSION in
        ubuntu:18.04|ubuntu:20.04|ubuntu:22.04|ubuntu:24.04) return 0 ;;
    esac
    case $OS_ID:$OS_MAJOR in
        rhel:8|rhel:9|almalinux:8|almalinux:9) return 0 ;;
    esac
    return 1
}

pkg_refresh() {
    if [[ $OS_FAMILY == debian ]]; then apt-get update -qq; else dnf -q makecache > /dev/null; fi
}

# pkg_install <package>... - packages that do not exist on this release are skipped (checked afterwards).
pkg_install() {
    if [[ $OS_FAMILY == debian ]]; then
        local available=() pkg
        for pkg in "$@"; do
            apt-cache show "$pkg" > /dev/null 2>&1 && available+=("$pkg")
        done
        DEBIAN_FRONTEND=noninteractive apt-get install -y -qq "${available[@]}" > /dev/null
    else
        dnf install -y -q --setopt=strict=0 "$@"
    fi
}

pkg_remove() {
    if [[ $OS_FAMILY == debian ]]; then
        DEBIAN_FRONTEND=noninteractive apt-get purge -y -qq "$@" > /dev/null
    else
        dnf remove -y "$@"
    fi
}

# Version of the Dovecot package the system would install, without epoch (e.g. 2.3.16).
dovecot_candidate_version() {
    if [[ $OS_FAMILY == debian ]]; then
        apt-cache policy dovecot-core | awk '/Candidate:/ { print $2 }' | sed 's/^[0-9]*://'
    else
        dnf -q repoquery --latest-limit 1 --qf '%{version}' dovecot 2>/dev/null | tail -n1
    fi
}

# EPEL provides OpenDKIM, certbot and swaks on RHEL/AlmaLinux; some EPEL packages need CRB.
enable_epel() {
    rpm -q epel-release > /dev/null 2>&1 && return 0
    info "Enabling EPEL"
    if [[ $OS_ID == rhel ]]; then
        if command -v subscription-manager > /dev/null; then
            subscription-manager repos --enable "codeready-builder-for-rhel-$OS_MAJOR-$(uname -m)-rpms" > /dev/null \
                || warn "Could not enable CodeReady Builder; continuing without it."
        fi
        dnf install -y -q "https://dl.fedoraproject.org/pub/epel/epel-release-latest-$OS_MAJOR.noarch.rpm"
    else
        dnf install -y -q epel-release dnf-plugins-core
        if [[ $OS_MAJOR == 8 ]]; then
            dnf config-manager --set-enabled powertools || warn "Could not enable PowerTools; continuing without it."
        else
            dnf config-manager --set-enabled crb || warn "Could not enable CRB; continuing without it."
        fi
    fi
}

selinux_on() {
    command -v selinuxenabled > /dev/null && selinuxenabled
}

# relabel <path>... - restore SELinux labels after creating files (no-op without SELinux).
relabel() {
    if selinux_on; then restorecon -R "$@"; fi
}

# Group for the static Dovecot userdb: Debian has "nogroup", RHEL only "nobody".
nobody_group() {
    if getent group nogroup > /dev/null; then echo nogroup; else echo nobody; fi
}

# ---------------------------------------------------------------------------------------------------
# Users (SMTP logins checked by Dovecot)
# ---------------------------------------------------------------------------------------------------

# set_user_password <address> <password> - adds the user or replaces its password.
set_user_password() {
    local user=$1 password=$2 hash tmp
    hash=$(doveadm pw -s SHA512-CRYPT -p "$password")
    tmp=$(mktemp)
    if [[ -f $USERS_FILE ]]; then
        awk -F: -v u="$user" '$1 != u' "$USERS_FILE" > "$tmp"
    fi
    printf '%s:%s\n' "$user" "$hash" >> "$tmp"
    install -m 640 -o root -g dovecot "$tmp" "$USERS_FILE"
    rm -f "$tmp"
    relabel "$USERS_FILE"
}

user_exists() {
    [[ -f $USERS_FILE ]] && awk -F: -v u="$1" '$1 == u { found = 1 } END { exit !found }' "$USERS_FILE"
}

# read_or_generate_password - prints the password to use (empty input = random).
read_or_generate_password() {
    local p1 p2
    read -rsp "Password (leave empty to generate one): " p1; echo >&2
    if [[ -z $p1 ]]; then
        random_password
        return
    fi
    read -rsp "Repeat password: " p2; echo >&2
    [[ $p1 == "$p2" ]] || die "Passwords do not match."
    [[ ${#p1} -ge 12 ]] || die "Use at least 12 characters."
    printf '%s' "$p1"
}

cmd_add_user() {
    require_root; require_installed
    local user=${1:-} password
    [[ -n $user ]] || ask user "SMTP login (email address)" "no-reply@$MAIL_DOMAIN"
    valid_address "$user" || die "Not an email address: $user"
    [[ ${user#*@} == "$MAIL_DOMAIN" ]] || warn "$user is not @$MAIL_DOMAIN; SPF/DKIM only cover $MAIL_DOMAIN."
    user_exists "$user" && die "$user already exists. Use: $0 passwd $user"
    password=$(read_or_generate_password)
    set_user_password "$user" "$password"
    ok "Created SMTP login $user"
    print_app_settings "$user" "$password"
}

cmd_passwd() {
    require_root; require_installed
    local user=${1:-} password
    [[ -n $user ]] || ask user "SMTP login to change"
    user_exists "$user" || die "No such login: $user"
    password=$(read_or_generate_password)
    set_user_password "$user" "$password"
    ok "Password changed for $user. Update Smtp__Password wherever the SAWP API runs."
    printf '\n  Smtp__Password=%s\n\n' "$password"
}

cmd_del_user() {
    require_root; require_installed
    local user=${1:-} tmp
    [[ -n $user ]] || ask user "SMTP login to delete"
    user_exists "$user" || die "No such login: $user"
    confirm "Delete SMTP login $user?" || return 0
    tmp=$(mktemp)
    awk -F: -v u="$user" '$1 != u' "$USERS_FILE" > "$tmp"
    install -m 640 -o root -g dovecot "$tmp" "$USERS_FILE"
    rm -f "$tmp"
    relabel "$USERS_FILE"
    ok "Deleted $user"
}

cmd_list_users() {
    require_root; require_installed
    if [[ ! -s $USERS_FILE ]]; then
        echo "(no SMTP logins)"
        return
    fi
    cut -d: -f1 "$USERS_FILE"
}

print_app_settings() {
    local user=$1 password=$2
    cat <<EOF

Settings for the SAWP API (environment variables; store the password now, it is not saved in plain text):

  Smtp__Host=$MAIL_HOST
  Smtp__Port=587
  Smtp__Security=StartTls
  Smtp__Username=$user
  Smtp__Password=$password
  Smtp__FromAddress=$user
  Smtp__FromName=SAWP

EOF
}

# ---------------------------------------------------------------------------------------------------
# Install
# ---------------------------------------------------------------------------------------------------

preflight() {
    if ! os_is_supported; then
        warn "This script supports Ubuntu 18.04-24.04 and RHEL/AlmaLinux 8-9; this is $OS_NAME."
        confirm "Continue anyway?" || exit 1
    fi
    if [[ $OS_ID == ubuntu && ( $OS_VERSION == 18.04 || $OS_VERSION == 20.04 ) ]]; then
        warn "$OS_NAME is past standard support: security updates need Ubuntu Pro (ESM)."
        warn "A mail server faces the internet; enable ESM ('pro attach') or upgrade the release."
    fi
    pkg_refresh
    local dovecot_version
    dovecot_version=$(dovecot_candidate_version)
    # configure_dovecot writes 2.2/2.3 or 2.4 syntax; other versions are unknown.
    [[ $dovecot_version =~ ^2\.[234]\. ]] \
        || die "Dovecot ${dovecot_version:-package not found} is not 2.2-2.4; this script supports only those."
    info "Dovecot $dovecot_version will be used"
    if [[ -f $CONF_FILE ]]; then
        warn "Already installed ($CONF_FILE exists). Running install again rewrites the configuration."
        confirm "Continue?" || exit 0
    fi
}

check_outbound_25() {
    timeout 8 bash -c '</dev/tcp/gmail-smtp-in.l.google.com/25' 2>/dev/null
}

detect_ip() {
    ip -4 route get 1.1.1.1 2>/dev/null | awk '{ for (i = 1; i < NF; i++) if ($i == "src") { print $(i + 1); exit } }'
}

gather_answers() {
    echo
    echo "SAWP send-only mail server - installation"
    echo "Press Enter to accept the value in [brackets]."
    echo
    ask MAIL_DOMAIN "Domain to send from (e.g. example.edu.vn or thongbao.example.edu.vn)"
    valid_domain "$MAIL_DOMAIN" || die "Invalid domain: $MAIL_DOMAIN"
    ask MAIL_HOST "Mail server host name" "mail.$MAIL_DOMAIN"
    valid_domain "$MAIL_HOST" || die "Invalid host name: $MAIL_HOST"
    ask PUBLIC_IP "Public IPv4 address of this VPS" "$(detect_ip)"
    [[ $PUBLIC_IP =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Invalid IPv4 address: $PUBLIC_IP"
    if [[ $PUBLIC_IP =~ ^(10\.|172\.(1[6-9]|2[0-9]|3[01])\.|192\.168\.) ]]; then
        warn "$PUBLIC_IP is a private address. Enter the VPS's public IP if it is behind NAT."
    fi
    ask DKIM_SELECTOR "DKIM selector" "sawp"
    ask SMTP_USER "SMTP login for the SAWP API" "no-reply@$MAIL_DOMAIN"
    valid_address "$SMTP_USER" || die "Not an email address: $SMTP_USER"
    ask HAS_OTHER_MX "Does $MAIL_DOMAIN already receive email elsewhere (Google Workspace, Microsoft 365...)? (y/n)" "n"
    [[ $HAS_OTHER_MX =~ ^[YyNn]$ ]] || die "Answer y or n."
    echo
    echo "How should the TLS certificate for $MAIL_HOST be obtained?"
    echo "  standalone - certbot listens on port 80 itself (nothing else may use port 80)"
    echo "  nginx      - nginx already serves port 80 on this VPS"
    echo "  webroot    - another web server serves port 80; give its web root folder"
    local default_method=standalone
    if ss -ltn 'sport = :80' | grep -q LISTEN; then default_method=nginx; fi
    ask CERT_METHOD "Method" "$default_method"
    case $CERT_METHOD in
        standalone|nginx) ;;
        webroot) ask CERT_WEBROOT "Web root folder" "/var/www/html" ;;
        *) die "Unknown method: $CERT_METHOD" ;;
    esac
    ask CERT_EMAIL "Email for Let's Encrypt expiry notices" "admin@$MAIL_DOMAIN"
    echo
    echo "Summary:"
    echo "  From domain : $MAIL_DOMAIN"
    echo "  Host name   : $MAIL_HOST ($PUBLIC_IP)"
    echo "  DKIM        : $DKIM_SELECTOR._domainkey.$MAIL_DOMAIN"
    echo "  SMTP login  : $SMTP_USER"
    echo "  Certificate : $CERT_METHOD"
    echo
    echo "By continuing you agree to the Let's Encrypt Subscriber Agreement (https://letsencrypt.org/repository/)."
    confirm "Install now?" || exit 0
}

install_packages() {
    info "Installing packages"
    local packages cmd
    if [[ $OS_FAMILY == debian ]]; then
        debconf-set-selections <<EOF
postfix postfix/main_mailer_type select Internet Site
postfix postfix/mailname string $MAIL_DOMAIN
EOF
        packages=(postfix dovecot-core opendkim opendkim-tools certbot swaks dnsutils openssl)
    else
        enable_epel
        packages=(postfix dovecot opendkim opendkim-tools certbot swaks bind-utils openssl iproute
                  policycoreutils-python-utils)
    fi
    [[ $CERT_METHOD == nginx ]] && packages+=(python3-certbot-nginx)
    pkg_install "${packages[@]}"

    for cmd in postconf doveadm doveconf opendkim opendkim-genkey ss; do
        command -v "$cmd" > /dev/null || die "'$cmd' is missing after installing packages."
    done
    command -v certbot > /dev/null \
        || die "certbot is not available from this system's repositories. Install it (e.g. 'snap install --classic certbot') and run install again."
    for cmd in swaks dig opendkim-testkey; do
        command -v "$cmd" > /dev/null || warn "'$cmd' is not available; the check and test-send commands will skip what needs it."
    done
    ok "Packages installed"
}

obtain_certificate() {
    info "Requesting the Let's Encrypt certificate for $MAIL_HOST"
    local args=(certonly --non-interactive --agree-tos -m "$CERT_EMAIL" -d "$MAIL_HOST" --keep-until-expiring
                --server https://acme-v02.api.letsencrypt.org/directory)
    case $CERT_METHOD in
        standalone) args+=(--standalone) ;;
        nginx)      args+=(--nginx) ;;
        webroot)    args+=(--webroot -w "$CERT_WEBROOT") ;;
    esac
    if ! certbot "${args[@]}"; then
        fail "certbot failed. Check that $MAIL_HOST has an A record pointing to $PUBLIC_IP"
        fail "and that port 80 is reachable from the internet, then run install again."
        exit 1
    fi
    mkdir -p "$(dirname "$RENEW_HOOK")"
    cat > "$RENEW_HOOK" <<'EOF'
#!/bin/sh
# Installed by sawp-mail-server.sh: load the renewed certificate.
systemctl reload postfix
EOF
    chmod 755 "$RENEW_HOOK"
    # Debian/Ubuntu enable the renewal timer on install; EPEL's certbot does not.
    if [[ $OS_FAMILY == rhel ]]; then
        systemctl enable -q --now certbot-renew.timer \
            || warn "Could not enable certbot-renew.timer; set up 'certbot renew' to run daily."
    fi
    if selinux_on; then
        # Postfix may read files labelled cert_t.
        semanage fcontext -a -t cert_t '/etc/letsencrypt/(live|archive)(/.*)?' 2>/dev/null \
            || semanage fcontext -m -t cert_t '/etc/letsencrypt/(live|archive)(/.*)?'
        relabel /etc/letsencrypt
    fi
    ok "Certificate ready (renews automatically)"
}

configure_opendkim() {
    info "Configuring OpenDKIM"
    local key_dir=$DKIM_DIR/$MAIL_DOMAIN
    mkdir -p "$key_dir"
    if [[ ! -f $key_dir/$DKIM_SELECTOR.private ]]; then
        opendkim-genkey -b 2048 -d "$MAIL_DOMAIN" -s "$DKIM_SELECTOR" -D "$key_dir"
    fi
    chown -R opendkim:opendkim "$DKIM_DIR"
    chmod 700 "$key_dir"
    chmod 600 "$key_dir/$DKIM_SELECTOR.private"

    [[ -f /etc/opendkim.conf.orig ]] || cp /etc/opendkim.conf /etc/opendkim.conf.orig
    cat > /etc/opendkim.conf <<EOF
# Managed by sawp-mail-server.sh (original: /etc/opendkim.conf.orig)
Syslog                  yes
SyslogSuccess           yes
UMask                   007
UserID                  opendkim
PidFile                 /run/opendkim/opendkim.pid
Mode                    s
Canonicalization        relaxed/simple
OversignHeaders         From
Domain                  $MAIL_DOMAIN
Selector                $DKIM_SELECTOR
KeyFile                 $key_dir/$DKIM_SELECTOR.private
Socket                  inet:$DKIM_PORT@127.0.0.1
EOF
    # Debian/Ubuntu may pass the socket on the command line from /etc/default/opendkim.
    if [[ -f /etc/default/opendkim ]] && grep -q '^SOCKET=' /etc/default/opendkim; then
        sed -i "s|^SOCKET=.*|SOCKET=inet:$DKIM_PORT@127.0.0.1|" /etc/default/opendkim
        if [[ -x /lib/opendkim/opendkim.service.generate ]]; then
            /lib/opendkim/opendkim.service.generate
            systemctl daemon-reload
        fi
    fi
    relabel /etc/opendkim /etc/opendkim.conf
    systemctl enable -q opendkim
    systemctl restart opendkim
    sleep 1
    ss -ltn "sport = :$DKIM_PORT" | grep -q LISTEN \
        || die "OpenDKIM is not listening on 127.0.0.1:$DKIM_PORT. See: journalctl -u opendkim"
    ok "OpenDKIM signs mail for $MAIL_DOMAIN"
}

# Installed Dovecot version without build suffix, e.g. "2.4.1" from "2.4.1-4 (7d8c0e5759)".
dovecot_installed_version() {
    dovecot --version | awk '{ print $1 }' | cut -d- -f1
}

# Self-contained configuration: no IMAP/POP3, no system-account logins, only the auth socket for
# Postfix. Dovecot 2.4 changed the syntax, so write the variant matching the installed version.
write_dovecot_conf() {
    local version=$1 group=$2
    if [[ $version =~ ^2\.4\. ]]; then
        cat <<EOF
dovecot_config_version = $version
dovecot_storage_version = $version
# Managed by sawp-mail-server.sh for Dovecot 2.4 (original: /etc/dovecot/dovecot.conf.orig)
# No protocols: 2.4 enables none by default.
ssl = no
log_path = syslog
auth_mechanisms = plain login
auth_allow_cleartext = no

passdb passwd-file {
  passwd_file_path = $USERS_FILE
  default_password_scheme = SHA512-CRYPT
}
userdb static {
  fields {
    uid = nobody
    gid = $group
    home = /nonexistent
  }
}
EOF
    else
        cat <<EOF
# Managed by sawp-mail-server.sh for Dovecot 2.2/2.3 (original: /etc/dovecot/dovecot.conf.orig)
protocols = none
ssl = no
log_path = syslog
auth_mechanisms = plain login
disable_plaintext_auth = yes

passdb {
  driver = passwd-file
  args = scheme=SHA512-CRYPT username_format=%u $USERS_FILE
}
userdb {
  driver = static
  args = uid=nobody gid=$group home=/nonexistent
}
EOF
    fi
    # The same in all versions: the socket Postfix asks for SMTP logins.
    cat <<EOF

service auth {
  unix_listener /var/spool/postfix/private/auth {
    mode = 0660
    user = postfix
    group = postfix
  }
}
EOF
}

configure_dovecot() {
    local version
    version=$(dovecot_installed_version)
    info "Configuring Dovecot $version (SMTP login check only, no mailboxes)"
    [[ -f /etc/dovecot/dovecot.conf.orig ]] || cp /etc/dovecot/dovecot.conf /etc/dovecot/dovecot.conf.orig
    write_dovecot_conf "$version" "$(nobody_group)" > /etc/dovecot/dovecot.conf
    if [[ ! -f $USERS_FILE ]]; then
        install -m 640 -o root -g dovecot /dev/null "$USERS_FILE"
    fi
    relabel /etc/dovecot
    doveconf -n > /dev/null || die "Dovecot configuration is invalid (doveconf -n)."
    systemctl enable -q dovecot
    systemctl restart dovecot
    ok "Dovecot ready"
}

configure_postfix() {
    info "Configuring Postfix"
    local cert_dir=/etc/letsencrypt/live/$MAIL_HOST chroot
    [[ $OS_FAMILY == debian ]] && echo "$MAIL_DOMAIN" > /etc/mailname
    postconf -e \
        "myhostname = $MAIL_HOST" \
        "mydomain = $MAIL_DOMAIN" \
        "myorigin = \$mydomain" \
        "inet_interfaces = all" \
        "inet_protocols = ipv4" \
        "mydestination = localhost" \
        "mynetworks = 127.0.0.0/8" \
        "smtpd_banner = \$myhostname ESMTP" \
        "smtpd_tls_cert_file = $cert_dir/fullchain.pem" \
        "smtpd_tls_key_file = $cert_dir/privkey.pem" \
        "smtpd_tls_security_level = may" \
        "smtpd_tls_protocols = !SSLv2, !SSLv3, !TLSv1, !TLSv1.1" \
        "smtpd_tls_mandatory_protocols = !SSLv2, !SSLv3, !TLSv1, !TLSv1.1" \
        "smtp_tls_security_level = may" \
        "smtp_tls_loglevel = 1" \
        "smtpd_sasl_type = dovecot" \
        "smtpd_sasl_path = private/auth" \
        "smtpd_sasl_auth_enable = no" \
        "smtpd_relay_restrictions = permit_mynetworks, permit_sasl_authenticated, defer_unauth_destination" \
        "milter_default_action = accept" \
        "milter_protocol = 6" \
        "smtpd_milters = inet:127.0.0.1:$DKIM_PORT" \
        "non_smtpd_milters = \$smtpd_milters"

    # Port 587: STARTTLS required, login required, relays only for logged-in clients.
    # Chroot like the existing port 25 service (Debian: chrooted; RHEL: not, and has no chroot files).
    chroot=$(postconf -Fh smtp/inet/chroot 2>/dev/null || echo -)
    postconf -M "submission/inet=submission inet n - $chroot - - smtpd"
    postconf -P \
        "submission/inet/syslog_name=postfix/submission" \
        "submission/inet/smtpd_tls_security_level=encrypt" \
        "submission/inet/smtpd_sasl_auth_enable=yes" \
        "submission/inet/smtpd_tls_auth_only=yes" \
        "submission/inet/smtpd_client_restrictions=permit_sasl_authenticated,reject" \
        "submission/inet/smtpd_relay_restrictions=permit_sasl_authenticated,reject"

    postfix check || die "postfix check reported errors."
    systemctl enable -q postfix
    systemctl restart postfix
    ok "Postfix ready on ports 25 and 587"
}

configure_firewall() {
    if command -v ufw > /dev/null && ufw status | grep -q '^Status: active'; then
        info "Opening firewall ports (ufw)"
        ufw allow 587/tcp > /dev/null
        [[ $HAS_OTHER_MX =~ ^[Nn]$ ]] && ufw allow 25/tcp > /dev/null
        [[ $CERT_METHOD == standalone ]] && ufw allow 80/tcp > /dev/null
        ok "ufw rules added"
    elif command -v firewall-cmd > /dev/null && firewall-cmd --state > /dev/null 2>&1; then
        info "Opening firewall ports (firewalld)"
        firewall-cmd -q --permanent --add-service=smtp-submission
        [[ $HAS_OTHER_MX =~ ^[Nn]$ ]] && firewall-cmd -q --permanent --add-service=smtp
        [[ $CERT_METHOD == standalone ]] && firewall-cmd -q --permanent --add-service=http
        firewall-cmd -q --reload
        ok "firewalld rules added"
    else
        warn "No active firewall (ufw/firewalld). Make sure the VPS provider's firewall allows inbound TCP 587"
        warn "(and 25 if MX points here, 80 for certificate renewal with the standalone method)."
    fi
}

save_config() {
    mkdir -p "$CONF_DIR"
    cat > "$CONF_FILE" <<EOF
# Written by sawp-mail-server.sh install
MAIL_DOMAIN=$MAIL_DOMAIN
MAIL_HOST=$MAIL_HOST
PUBLIC_IP=$PUBLIC_IP
DKIM_SELECTOR=$DKIM_SELECTOR
SMTP_USER=$SMTP_USER
HAS_OTHER_MX=$HAS_OTHER_MX
CERT_METHOD=$CERT_METHOD
EOF
    chmod 600 "$CONF_FILE"
}

cmd_install() {
    require_root
    preflight
    gather_answers
    if ! check_outbound_25; then
        warn "Outbound port 25 seems blocked: this server cannot deliver mail to other servers."
        warn "Ask the VPS provider to unblock it, or use an SMTP provider (docs/EMAIL_SERVER_SETUP.md, section 9)."
        confirm "Install anyway?" || exit 1
    fi
    install_packages
    configure_firewall
    obtain_certificate
    configure_opendkim
    configure_dovecot
    configure_postfix
    save_config

    local password
    if user_exists "$SMTP_USER"; then
        ok "SMTP login $SMTP_USER already exists (password unchanged)"
        password="<unchanged - use '$0 passwd $SMTP_USER' to set a new one>"
    else
        password=$(random_password)
        set_user_password "$SMTP_USER" "$password"
        ok "Created SMTP login $SMTP_USER"
    fi

    echo
    ok "Installation finished."
    cmd_dns
    print_app_settings "$SMTP_USER" "$password"
    echo "Next: create the DNS records above, wait for them to update, then run:"
    echo "  $0 check"
    echo "  $0 test-send your-own-address@gmail.com"
}

# ---------------------------------------------------------------------------------------------------
# DNS
# ---------------------------------------------------------------------------------------------------

cmd_dns() {
    require_root; require_installed
    local dkim spf_value mx_line="" ptr a spf dmarc dkim_dns
    dkim=$(dkim_txt_value)
    spf_value="v=spf1 ip4:$PUBLIC_IP -all"
    if [[ $HAS_OTHER_MX =~ ^[Nn]$ ]]; then
        mx_line=$(printf '  %-5s %-40s %s' MX "$MAIL_DOMAIN" "10 $MAIL_HOST")
    fi

    echo
    echo "DNS records to create (at the DNS provider of $MAIL_DOMAIN):"
    echo
    printf '  %-5s %-40s %s\n' A "$MAIL_HOST" "$PUBLIC_IP"
    [[ -n $mx_line ]] && echo "$mx_line"
    printf '  %-5s %-40s %s\n' TXT "$MAIL_DOMAIN" "$spf_value"
    printf '  %-5s %-40s %s\n' TXT "$DKIM_SELECTOR._domainkey.$MAIL_DOMAIN" "$dkim"
    printf '  %-5s %-40s %s\n' TXT "_dmarc.$MAIL_DOMAIN" "v=DMARC1; p=none; rua=mailto:dmarc@$MAIL_DOMAIN"
    echo
    echo "At the VPS provider (reverse DNS):"
    printf '  %-5s %-40s %s\n' PTR "$PUBLIC_IP" "$MAIL_HOST"
    echo
    if [[ $HAS_OTHER_MX =~ ^[Yy]$ ]]; then
        echo "The domain already receives email elsewhere: keep its MX records, and if it already has a"
        echo "'v=spf1' record, add 'ip4:$PUBLIC_IP' to it instead of creating a second one."
        echo
    fi

    if ! command -v dig > /dev/null; then
        warn "dig is not installed; cannot check the published records."
        return 0
    fi
    echo "Current DNS:"
    a=$(dig +short A "$MAIL_HOST" | tail -n1)
    [[ $a == "$PUBLIC_IP" ]] && ok "A    $MAIL_HOST -> $a" || warn "A    $MAIL_HOST -> ${a:-missing} (want $PUBLIC_IP)"
    ptr=$(dig +short -x "$PUBLIC_IP" | sed 's/\.$//' | head -n1)
    [[ $ptr == "$MAIL_HOST" ]] && ok "PTR  $PUBLIC_IP -> $ptr" || warn "PTR  $PUBLIC_IP -> ${ptr:-missing} (want $MAIL_HOST)"
    spf=$(dig +short TXT "$MAIL_DOMAIN" | tr -d '"' | grep '^v=spf1' || true)
    if [[ $(grep -c '^v=spf1' <<< "$spf" || true) -gt 1 ]]; then
        warn "SPF  more than one v=spf1 record - merge them into one"
    elif [[ $spf == *"ip4:$PUBLIC_IP"* ]]; then
        ok "SPF  $spf"
    else
        warn "SPF  ${spf:-missing} (must include ip4:$PUBLIC_IP)"
    fi
    dkim_dns=$(dig +short TXT "$DKIM_SELECTOR._domainkey.$MAIL_DOMAIN" | tr -d '" \n')
    if [[ -n $dkim_dns && $dkim_dns == "$(tr -d ' ' <<< "$dkim")" ]]; then
        ok "DKIM $DKIM_SELECTOR._domainkey.$MAIL_DOMAIN matches the key"
    else
        warn "DKIM $DKIM_SELECTOR._domainkey.$MAIL_DOMAIN ${dkim_dns:+does not match the key}${dkim_dns:-missing}"
    fi
    dmarc=$(dig +short TXT "_dmarc.$MAIL_DOMAIN" | tr -d '"' | grep '^v=DMARC1' || true)
    [[ -n $dmarc ]] && ok "DMARC $dmarc" || warn "DMARC missing"
    echo
}

# ---------------------------------------------------------------------------------------------------
# Check and test
# ---------------------------------------------------------------------------------------------------

cmd_check() {
    require_root; require_installed
    local errors=0 svc port cert end relay_reply

    for svc in postfix dovecot opendkim; do
        if systemctl is-active -q "$svc"; then ok "$svc running"; else fail "$svc not running"; errors=$((errors + 1)); fi
    done
    for port in 25 587 $DKIM_PORT; do
        if ss -ltn "sport = :$port" | grep -q LISTEN; then ok "port $port listening"; else fail "port $port not listening"; errors=$((errors + 1)); fi
    done

    cert=/etc/letsencrypt/live/$MAIL_HOST/fullchain.pem
    if [[ -f $cert ]]; then
        end=$(openssl x509 -enddate -noout -in "$cert" | cut -d= -f2)
        if openssl x509 -checkend $((14 * 86400)) -noout -in "$cert" > /dev/null; then
            ok "certificate valid until $end"
        else
            fail "certificate expires soon ($end); run: certbot renew"; errors=$((errors + 1))
        fi
    else
        fail "certificate missing: $cert"; errors=$((errors + 1))
    fi

    if postfix check 2>/dev/null; then ok "postfix configuration"; else fail "postfix check failed"; errors=$((errors + 1)); fi
    if ! command -v opendkim-testkey > /dev/null; then
        warn "opendkim-testkey not installed; DKIM key check skipped"
    elif opendkim-testkey -d "$MAIL_DOMAIN" -s "$DKIM_SELECTOR" -k "$DKIM_DIR/$MAIL_DOMAIN/$DKIM_SELECTOR.private" 2>/dev/null; then
        ok "DKIM key published and matching"
    else
        fail "DKIM key not published or not matching (see: $0 dns)"; errors=$((errors + 1))
    fi
    if check_outbound_25; then ok "outbound port 25 open"; else fail "outbound port 25 blocked by the VPS provider"; errors=$((errors + 1)); fi

    # Without a login, port 587 must refuse to relay: look at the server's reply to RCPT TO.
    if ! command -v swaks > /dev/null; then
        fail "swaks not installed; open-relay test skipped"; errors=$((errors + 1))
        relay_reply="skipped"
    else
        relay_reply=$(swaks --server 127.0.0.1 --port 587 --tls --quit-after RCPT \
        --from "check@$MAIL_DOMAIN" --to relay-test@example.com 2>&1 \
        | awk '/RCPT TO/ { getline; print; exit }' || true)
    fi
    if [[ $relay_reply == skipped ]]; then
        :
    elif [[ $relay_reply =~ [[:space:]]5[0-9][0-9][[:space:]] ]]; then
        ok "port 587 refuses to relay without login"
    elif [[ $relay_reply =~ [[:space:]]250[[:space:]] ]]; then
        fail "port 587 accepted a recipient without login (open relay!)"; errors=$((errors + 1))
    else
        fail "relay test inconclusive: ${relay_reply:-no reply to RCPT TO}"; errors=$((errors + 1))
    fi

    cmd_dns
    if [[ $errors -eq 0 ]]; then ok "All checks passed"; else fail "$errors check(s) failed"; return 1; fi
}

cmd_test_send() {
    require_root; require_installed
    local to=${1:-} user password
    [[ -n $to ]] || ask to "Send a test email to"
    valid_address "$to" || die "Not an email address: $to"
    command -v swaks > /dev/null || die "swaks is not installed."
    user=${2:-$SMTP_USER}
    read -rsp "Password for $user: " password; echo
    swaks --server "$MAIL_HOST" --port 587 --tls \
        --auth LOGIN --auth-user "$user" --auth-password "$password" \
        --from "$user" --to "$to" \
        --header "Subject: SAWP mail server test" \
        --body "Test email from $MAIL_HOST. Check that SPF, DKIM and DMARC pass in the message headers."
    echo
    ok "Sent. In Gmail use 'Show original' and check SPF, DKIM and DMARC = PASS."
    echo "Delivery log: journalctl -u postfix -n 30"
}

# ---------------------------------------------------------------------------------------------------
# Uninstall
# ---------------------------------------------------------------------------------------------------

cmd_uninstall() {
    require_root; require_installed
    warn "This removes Postfix, Dovecot and OpenDKIM with their configuration, SMTP logins and DKIM keys."
    warn "The Let's Encrypt certificate is kept (remove it with: certbot delete --cert-name $MAIL_HOST)."
    confirm "Uninstall the mail server?" || return 0
    systemctl stop postfix dovecot opendkim 2>/dev/null || true
    if [[ $OS_FAMILY == debian ]]; then
        pkg_remove postfix dovecot-core opendkim opendkim-tools
    else
        pkg_remove postfix dovecot opendkim opendkim-tools
    fi
    rm -rf "$CONF_DIR" "$DKIM_DIR" /etc/dovecot /etc/opendkim.conf /etc/opendkim.conf.orig "$RENEW_HOOK"
    ok "Mail server removed. Delete its DNS records (DKIM, SPF entry, MX) if they are no longer used."
    echo "Firewall rules for ports 25/587/80 were left in place; remove them if nothing else needs them."
}

# ---------------------------------------------------------------------------------------------------
# Menu
# ---------------------------------------------------------------------------------------------------

usage() {
    sed -n '9,19p' "$0" | sed 's/^# \{0,1\}//'
}

menu() {
    require_root
    if [[ ! -f $CONF_FILE ]]; then
        cmd_install
        return
    fi
    # shellcheck source=/dev/null
    source "$CONF_FILE"
    echo
    echo "SAWP mail server ($MAIL_HOST) is installed. What do you want to do?"
    echo "  1) Add an SMTP login"
    echo "  2) Change a password"
    echo "  3) Delete an SMTP login"
    echo "  4) List SMTP logins"
    echo "  5) Show and check DNS records"
    echo "  6) Run all checks"
    echo "  7) Send a test email"
    echo "  8) Uninstall"
    echo "  9) Exit"
    local choice
    read -rp "Choice [1-9]: " choice
    case $choice in
        1) cmd_add_user ;;
        2) cmd_passwd ;;
        3) cmd_del_user ;;
        4) cmd_list_users ;;
        5) cmd_dns ;;
        6) cmd_check ;;
        7) cmd_test_send ;;
        8) cmd_uninstall ;;
        *) exit 0 ;;
    esac
}

main() {
    local cmd=${1:-}
    shift || true
    case $cmd in
        -h|--help|help) usage; return ;;
    esac
    detect_os
    case $cmd in
        "")          menu ;;
        install)     cmd_install ;;
        add-user)    cmd_add_user "$@" ;;
        passwd)      cmd_passwd "$@" ;;
        del-user)    cmd_del_user "$@" ;;
        list-users)  cmd_list_users ;;
        dns)         cmd_dns ;;
        check)       cmd_check ;;
        test-send)   cmd_test_send "$@" ;;
        uninstall)   cmd_uninstall ;;
        -h|--help|help) usage ;;
        *) usage; exit 1 ;;
    esac
}

main "$@"
