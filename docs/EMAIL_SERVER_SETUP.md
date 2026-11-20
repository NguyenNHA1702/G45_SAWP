# Email Server Setup (Production VPS)

How to run a **send-only** mail server on a Linux VPS so that the SAWP backend can email parents and staff,
and how to point the backend at it. The backend talks plain SMTP (MailKit), so it works with any standard
SMTP server: this self-hosted one, or a provider such as Google Workspace, Brevo or Amazon SES
(see [Using an SMTP provider instead](#9-using-an-smtp-provider-instead)).

```
SAWP API ──SMTP 587 + STARTTLS + login──▶ Postfix (mail.example.edu.vn) ──SMTP 25──▶ recipient's mail server
                                            │ OpenDKIM signs every message
                                            │ Dovecot checks the SMTP login
```

## Quick setup with the script

[`deploy/mail-server/sawp-mail-server.sh`](../deploy/mail-server/sawp-mail-server.sh) performs sections 3–6
in one run and manages the server afterwards. Copy it to the VPS and run it as root:

```bash
sudo ./sawp-mail-server.sh            # first run: asks a few questions and installs; later: menu
```

| Command | What it does |
| :--- | :--- |
| `install` | Installs Postfix, Dovecot, OpenDKIM and a Let's Encrypt certificate, creates the SMTP login, prints the DNS records and the `Smtp__*` settings for the API |
| `add-user <address>` / `passwd <address>` / `del-user <address>` / `list-users` | Manage SMTP logins (empty password = generated) |
| `dns` | Print the DNS records to create and check what is published |
| `check` | Services, ports, certificate expiry, DKIM key, outbound port 25, open-relay test, DNS |
| `test-send <to>` | Send a test email through port 587 with a login |
| `uninstall` | Remove the mail server (keeps the certificate) |

You still create the DNS records (section 2) and the PTR record yourself; `install` prints them and
`dns` shows which ones are missing. Running it again is safe: existing DKIM keys and logins are kept.

Supported systems:

| System | Notes |
| :--- | :--- |
| Ubuntu 22.04, 24.04 | Recommended |
| Ubuntu 18.04, 20.04 | Works (Dovecot 2.2/2.3), but these releases are past standard support: enable Ubuntu Pro (ESM) for security updates or upgrade |
| RHEL 8, 9 / AlmaLinux 8, 9 | Enables EPEL (and CodeReady Builder / CRB) for OpenDKIM, certbot and swaks; sets SELinux labels; opens ports in firewalld; enables `certbot-renew.timer` |
| Other Debian/RHEL derivatives (Debian 12/13, newer Ubuntu, Rocky, ...) | Asks for confirmation; not tested |
| Alpine, Arch, openSUSE | Not supported; the script stops before changing anything |

Dovecot is only used to check the SMTP login. The script reads the Dovecot version the system offers and
writes the matching configuration: 2.2/2.3 syntax or the new 2.4 syntax. Any other version stops the script.

The rest of this guide explains what the script configures, using Ubuntu commands, for manual setup and
troubleshooting.

Placeholders used below – replace them everywhere:

| Placeholder | Meaning |
| :--- | :--- |
| `example.edu.vn` | Domain the email is sent **from** (`no-reply@example.edu.vn`) |
| `mail.example.edu.vn` | Host name of the mail server |
| `203.0.113.10` | Public IPv4 address of the VPS |
| `sawp` | DKIM selector (any short name) |

Commands assume Ubuntu 24.04 LTS, run as root (or with `sudo`).

> **If the school domain already receives email** (e.g. Google Workspace or Microsoft 365), do not change its
> existing MX record, and do not add a second SPF record: merge the VPS IP into the existing one
> (section 2). Alternatively send from a subdomain such as `thongbao.example.edu.vn` and use that as the
> domain throughout this guide.

## 1. Prerequisites

- A VPS with a **static public IPv4** address.
- **Outbound port 25 open.** Many providers block it by default (DigitalOcean, Vultr, Linode, AWS, Azure,
  Google Cloud, new Hetzner accounts). Check with `nc -vz gmail-smtp-in.l.google.com 25` and, if it times
  out, ask the provider to unblock it. Without port 25 the server cannot deliver mail; use an SMTP provider
  instead (section 9).
- A **reverse DNS (PTR)** record you can set in the provider's control panel.
- Access to the domain's DNS zone.
- An IP that is not on a blocklist: check it at <https://mxtoolbox.com/blacklists.aspx>.

## 2. DNS records

Create these records at the DNS provider. TTL 3600 is fine; use 300 while testing.

| Type | Name | Value | Purpose |
| :--- | :--- | :--- | :--- |
| A | `mail.example.edu.vn` | `203.0.113.10` | Mail server address |
| MX | `example.edu.vn` | `10 mail.example.edu.vn` | Where bounces go. **Skip if the domain already has MX records.** |
| TXT | `example.edu.vn` | `v=spf1 ip4:203.0.113.10 -all` | SPF: only this IP may send for the domain |
| TXT | `sawp._domainkey.example.edu.vn` | from step 5 | DKIM public key |
| TXT | `_dmarc.example.edu.vn` | `v=DMARC1; p=none; rua=mailto:dmarc@example.edu.vn` | DMARC policy and reports |
| PTR | `203.0.113.10` | `mail.example.edu.vn` | Reverse DNS, set at the **VPS provider**, not the DNS provider |

Notes:

- **SPF:** a name may have only one `v=spf1` record. If one exists, add `ip4:203.0.113.10` to it, e.g.
  `v=spf1 include:_spf.google.com ip4:203.0.113.10 ~all`.
- **DMARC:** start with `p=none`. When reports show that SPF and DKIM pass for all real email, change it to
  `p=quarantine`, then `p=reject`.
- **PTR:** the PTR name, the A record and Postfix `myhostname` must all be `mail.example.edu.vn`. Gmail and
  Outlook reject or spam-folder mail from IPs without a matching PTR.

Check the records:

```bash
dig +short A mail.example.edu.vn
dig +short -x 203.0.113.10
dig +short TXT example.edu.vn
dig +short TXT _dmarc.example.edu.vn
```

## 3. Host name, firewall and TLS certificate

```bash
hostnamectl set-hostname mail.example.edu.vn
apt update && apt install -y certbot

ufw allow 22/tcp     # SSH – keep it open before enabling ufw
ufw allow 25/tcp     # inbound SMTP (bounces), only if MX points here
ufw allow 587/tcp    # submission port used by the SAWP API
ufw allow 80/tcp     # Let's Encrypt HTTP challenge
ufw enable
```

Get a Let's Encrypt certificate for the mail host name. With nothing listening on port 80:

```bash
certbot certonly --standalone -d mail.example.edu.vn \
  --deploy-hook "systemctl reload postfix"
```

If a web server (e.g. Nginx serving the SAWP frontend) already uses port 80 on this VPS, use
`certbot certonly --nginx -d mail.example.edu.vn --deploy-hook "systemctl reload postfix"` instead.
Certbot installs a timer that renews the certificate automatically; test it with `certbot renew --dry-run`.

Files used below:
`/etc/letsencrypt/live/mail.example.edu.vn/fullchain.pem` and `.../privkey.pem`.

## 4. Postfix (SMTP server)

```bash
DEBIAN_FRONTEND=noninteractive apt install -y postfix
```

Edit `/etc/postfix/main.cf` and set (replace existing keys, add missing ones):

```ini
myhostname = mail.example.edu.vn
mydomain = example.edu.vn
myorigin = $mydomain
inet_interfaces = all
inet_protocols = ipv4
# Send-only: do not accept mail for the domain itself.
mydestination = localhost
mynetworks = 127.0.0.0/8

# TLS for clients connecting to this server (the SAWP API)
smtpd_tls_cert_file = /etc/letsencrypt/live/mail.example.edu.vn/fullchain.pem
smtpd_tls_key_file = /etc/letsencrypt/live/mail.example.edu.vn/privkey.pem
smtpd_tls_security_level = may
smtpd_tls_protocols = !SSLv2, !SSLv3, !TLSv1, !TLSv1.1
smtpd_tls_mandatory_protocols = !SSLv2, !SSLv3, !TLSv1, !TLSv1.1

# TLS when delivering to other mail servers
smtp_tls_security_level = may
smtp_tls_loglevel = 1

# SMTP login is checked by Dovecot (section 6); enabled only on port 587 in master.cf
smtpd_sasl_type = dovecot
smtpd_sasl_path = private/auth
smtpd_sasl_auth_enable = no
smtpd_relay_restrictions = permit_mynetworks, permit_sasl_authenticated, defer_unauth_destination

# DKIM signing (section 5)
milter_default_action = accept
milter_protocol = 6
smtpd_milters = inet:127.0.0.1:8891
non_smtpd_milters = $smtpd_milters
```

In `/etc/postfix/master.cf`, enable the submission service (uncomment the `submission` line and set its
options as below):

```
submission inet n       -       y       -       -       smtpd
  -o syslog_name=postfix/submission
  -o smtpd_tls_security_level=encrypt
  -o smtpd_sasl_auth_enable=yes
  -o smtpd_tls_auth_only=yes
  -o smtpd_client_restrictions=permit_sasl_authenticated,reject
  -o smtpd_relay_restrictions=permit_sasl_authenticated,reject
```

Port 587 now requires STARTTLS and a login before it relays anything.

## 5. OpenDKIM (signing)

```bash
apt install -y opendkim opendkim-tools
mkdir -p /etc/opendkim/keys/example.edu.vn
opendkim-genkey -b 2048 -d example.edu.vn -s sawp -D /etc/opendkim/keys/example.edu.vn
chown -R opendkim:opendkim /etc/opendkim/keys
chmod 600 /etc/opendkim/keys/example.edu.vn/sawp.private
```

Edit `/etc/opendkim.conf` and set:

```ini
Syslog                  yes
UMask                   007
Mode                    s
Canonicalization        relaxed/simple
Domain                  example.edu.vn
Selector                sawp
KeyFile                 /etc/opendkim/keys/example.edu.vn/sawp.private
Socket                  inet:8891@127.0.0.1
```

Comment out any other `Socket` line in that file. If `/etc/default/opendkim` sets `SOCKET=`, set it to
`inet:8891@127.0.0.1` there too.

Publish the public key: `cat /etc/opendkim/keys/example.edu.vn/sawp.txt` prints the TXT record for
`sawp._domainkey`. Join the quoted parts into one value (`v=DKIM1; h=sha256; k=rsa; p=MIIBIjAN...`) and
create the TXT record from section 2. Most DNS panels split long values automatically.

```bash
systemctl restart opendkim
ss -ltnp | grep 8891                                   # OpenDKIM is listening
opendkim-testkey -d example.edu.vn -s sawp -vvv        # expect "key OK" once DNS has updated
```

## 6. Dovecot (SMTP login only)

Dovecot is used only to check the username and password; no IMAP/POP3 mailboxes are created.

```bash
apt install -y dovecot-core
```

In `/etc/dovecot/conf.d/10-auth.conf`:

```
disable_plaintext_auth = yes
auth_mechanisms = plain login
#!include auth-system.conf.ext
!include auth-passwdfile.conf.ext
```

Create `/etc/dovecot/conf.d/auth-passwdfile.conf.ext` with:

```
passdb {
  driver = passwd-file
  args = scheme=SHA512-CRYPT username_format=%u /etc/dovecot/users
}
userdb {
  driver = static
  args = uid=nobody gid=nogroup home=/nonexistent
}
```

In `/etc/dovecot/conf.d/10-master.conf`, inside `service auth { ... }`, add the socket Postfix uses:

```
  unix_listener /var/spool/postfix/private/auth {
    mode = 0660
    user = postfix
    group = postfix
  }
```

Create the account the SAWP API logs in with (use a long random password):

```bash
HASH=$(doveadm pw -s SHA512-CRYPT)        # prompts for the password twice
echo "no-reply@example.edu.vn:$HASH" > /etc/dovecot/users
chown root:dovecot /etc/dovecot/users
chmod 640 /etc/dovecot/users

systemctl restart dovecot postfix
```

## 7. Test the server

From any machine:

```bash
apt install -y swaks
swaks --to your-own-address@gmail.com --from no-reply@example.edu.vn \
  --server mail.example.edu.vn --port 587 --tls \
  --auth LOGIN --auth-user no-reply@example.edu.vn --auth-password '<password>'
```

- In Gmail open the message, choose **Show original**, and check `SPF: PASS`, `DKIM: PASS`, `DMARC: PASS`.
- Send a message to the address shown on <https://www.mail-tester.com> and aim for 9/10 or better.
- Logs: `journalctl -u postfix -f` (or `/var/log/mail.log`).
- Make sure it is **not an open relay**: without the `--auth` options, the same `swaks` command must be
  refused (`554 5.7.1 ... Access denied`); without `--tls`, the login must be refused
  (`530 5.7.0 Must issue a STARTTLS command first`).

## 8. Configure the SAWP backend

Set the `Smtp` settings with environment variables on the machine (or container) that runs the API. Never
put the password in `appsettings*.json` in Git. ASP.NET Core maps `Smtp__Host` to `Smtp:Host`.

```bash
Smtp__Host=mail.example.edu.vn
Smtp__Port=587
Smtp__Security=StartTls
Smtp__Username=no-reply@example.edu.vn
Smtp__Password=<password>
Smtp__FromAddress=no-reply@example.edu.vn
Smtp__FromName='Trường Tiểu học ...'
```

- `Smtp__Host` must be the name on the certificate (`mail.example.edu.vn`), even if the API runs on the same
  VPS; `localhost` would fail the TLS name check.
- `FromAddress` must use the domain covered by SPF/DKIM, otherwise DMARC fails.
- A wrong setting shows up in the API log as a MailKit exception (`AuthenticationException`,
  `SslHandshakeException`, `SocketException`).

For a systemd service, put the variables in an `EnvironmentFile=` readable only by the service user.

## 9. Using an SMTP provider instead

If outbound port 25 cannot be opened, or nobody can maintain a mail server, use a provider. Only the
backend settings change; SPF/DKIM records are then the ones the provider gives you.

| Provider | `Host` | `Port` / `Security` | Login |
| :--- | :--- | :--- | :--- |
| Google Workspace / Gmail | `smtp.gmail.com` | `587` / `StartTls` | Account address + app password (requires 2-step verification) |
| Brevo | `smtp-relay.brevo.com` | `587` / `StartTls` | SMTP login and key from the Brevo dashboard |
| Amazon SES | `email-smtp.<region>.amazonaws.com` | `587` / `StartTls` | SES SMTP credentials |

## 10. Maintenance checklist

- Certificate renews automatically; Postfix reloads through the certbot deploy hook.
- `apt upgrade` regularly; `unattended-upgrades` is recommended.
- Read DMARC reports (sent to the `rua` address) and tighten `p=` once everything passes.
- Watch `/var/log/mail.log` for `status=bounced` and `status=deferred`.
- Rotate the SMTP password by regenerating the line in `/etc/dovecot/users` and updating `Smtp__Password`.
