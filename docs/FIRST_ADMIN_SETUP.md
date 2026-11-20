# First Admin Account (Production)

How the first Admin account of a new SAWP installation is created, how to keep it safe, and how to get
back in if the last Admin is locked out.

Every other account (school board, office staff, teachers, service staff, parents) is created by an Admin in
the app (**Quản lý tài khoản**, `/admin/users`) or by the staff import. Only the first Admin needs this
procedure, because creating users requires an Admin login.

## 1. How it works

On every start the API runs these steps in [`Program.cs`](../SAWP.Backend/API/Program.cs), in order:

1. Apply pending EF Core migrations.
2. Seed the six roles (`SeedRolesAsync`). Every environment.
3. Seed the demo data (`SeedDemoDataAsync`), only in Development or when `Seed:DemoData` is `true`.
   The demo data contains an `admin` account with the shared password `Password@123`; never enable it in
   production.
4. Create the first Admin from configuration (`SeedFirstAdminAsync` in
   [`DbInitializer.cs`](../SAWP.Backend/Infrastructure/Data/DbInitializer.cs)).

Step 4 asks three questions, in this order, and stops at the first one that settles it:

**a) Does any account already have the Admin role?** (Active or deactivated.)
Yes → step 4 does nothing and the API starts normally. `Seed:Admin` is never used to change an existing
account. If `Seed:Admin:Password` is still set, the log warns you to remove it:
`An admin account already exists, so Seed:Admin is ignored ...`

**b) Is `Seed:Admin` set at all?** (Any of Username, Email or Password.)
No → step 4 does nothing and the API starts, but nobody can log in. The log warns:
`No admin account exists. Set Seed:Admin:Username, ...`

**c) Are the settings usable?** All three must hold, otherwise the API **stops** with the message shown, so
you fix the setting and start it again:

| Check | Message when it fails |
| :--- | :--- |
| Username, Email and Password are all set | `Seed:Admin: Username, Email and Password are all required ...` |
| Password meets the password policy | `Seed:Admin:Password must have at least 8 characters ...` |
| No other account uses that username or email | `Seed:Admin: the username or email already belongs to another account.` |

All three hold → step 4 creates one active account with the Admin role (password stored as a BCrypt hash),
and the log says `Created the first admin account '<username>' from Seed:Admin ...`

So the account is created exactly once. From then on question a) is always "yes": changing
`Seed:Admin:Password` later does **not** change the account's password; use the app for that (section 4).

## 2. Settings

ASP.NET Core maps `Seed__Admin__Password` (environment variable) to `Seed:Admin:Password`.

| Setting | Required | Notes |
| :--- | :--- | :--- |
| `Seed__Admin__Username` | Yes | Login name. Avoid `admin`: it is the name everyone guesses first. |
| `Seed__Admin__Email` | Yes | A mailbox the responsible person reads. Must not belong to another account. |
| `Seed__Admin__Password` | Yes | Password policy: at least 8 characters with an upper-case letter, a lower-case letter, a digit and a special character. Use a long generated password. |
| `Seed__Admin__FullName` | No | Display name. Default `Quản trị viên Hệ thống`. |

The API also needs `Jwt__Key` (at least 32 bytes) in every environment except Development; without it the
API stops before step 1. See "Startup Data and Secrets" in the [README](../README.md).

Never put these values in `appsettings*.json` or any other file in Git. `appsettings.Local.json` is not
published (`API.csproj` excludes it), so it cannot carry them to the server either.

## 3. First start

On the machine (or container) that runs the API:

1. Generate the secrets:

   ```bash
   openssl rand -base64 48              # Jwt__Key
   openssl rand -base64 24              # basis for the admin password
   ```

   Make sure the admin password contains an upper-case letter, a lower-case letter, a digit and a special
   character (append e.g. `#7aB` if the generated text lacks one).

2. Set the variables. For a systemd service, use an `EnvironmentFile=` readable only by the service user:

   ```bash
   # /etc/sawp/api.env  (chmod 600, owned by the service user)
   ASPNETCORE_ENVIRONMENT=Production
   Jwt__Key=<generated key>
   Seed__Admin__Username=<login name>
   Seed__Admin__Email=<email>
   Seed__Admin__Password=<password>
   ```

3. Start the API and check the log for
   `Created the first admin account '<login name>' from Seed:Admin.`
   If the API stopped instead, the log names the setting to fix (table in section 1).

4. **Remove `Seed__Admin__Password`** (and the other `Seed__Admin__*` lines) from the environment file and
   restart. Keep `Jwt__Key`: changing it logs everyone out.

5. Log in with the new account and change its password at once (**Hồ sơ cá nhân** → **Đổi mật khẩu tài
   khoản**). The password that sat in the environment file must not stay in use.

6. Hand the password to the responsible person in person or through a password manager, never by chat
   or email.

## 4. Keeping the Admin account safe

- **Two Admins.** Create a second Admin for another responsible person in **Quản lý tài khoản**, so one
  can reset the other's password. An Admin cannot deactivate their own account or remove their own Admin
  role, but one Admin can do both to another.
- **Personal accounts.** One account per person, no shared Admin login, so the audit log shows who did what.
- **Admin role only where needed.** Staff who only need Office Staff or School Board functions get those
  roles, not Admin.
- **Leaving staff.** Deactivate the account (do not delete it) when an Admin leaves the school.
- **Password resets.** An Admin resets another user's password in **Quản lý tài khoản**; this is
  recorded in the audit log (`ADMIN_RESET_PASSWORD`). The first-admin creation itself is not in the audit
  log, only in the API log.

Known gaps (not implemented yet):

- Parent accounts created with a student, by the student import, and staff imported without a password all
  get the same default password `Password@123`.
- No "change password at next login" option, which the SRS requires for temporary passwords (pub:BR-02,
  pub:UC-60 A1). Until it exists, tell every user to change a default or reset password at once.
- No lockout or rate limit on failed logins.

A password stored as plain text (e.g. written to `Users.PasswordHash` by hand) never works: the login and the
password change reject it and the API log warns `User <id> has a stored password that is not a BCrypt hash`.
Fix such an account with a password reset in **Quản lý tài khoản**, never with SQL.

Until they are fixed, long passwords and keeping the API behind HTTPS matter even more.

## 5. Recovery: no Admin can log in

Step 4 never touches an existing Admin, so it cannot reset a forgotten password. Options, simplest first:

1. **Another Admin exists:** that Admin resets the password in **Quản lý tài khoản**.
2. **No other Admin:** take the Admin role away from the locked account in the database, then let the
   API create a new first Admin:

   ```sql
   -- Run against the SAWP database. Replace the login name.
   DELETE ur
   FROM UserRoles ur
   JOIN Users u ON u.Id = ur.UserId
   JOIN Roles r ON r.Id = ur.RoleId
   WHERE u.Username = N'<locked login name>' AND r.Name = N'Admin';
   ```

   Make sure no other row with the Admin role is left, otherwise step 4 still sees an Admin:

   ```sql
   SELECT u.Username, u.IsActive
   FROM Users u
   JOIN UserRoles ur ON ur.UserId = u.Id
   JOIN Roles r ON r.Id = ur.RoleId
   WHERE r.Name = N'Admin';
   ```

   Then follow section 3 with a **different** username and email (the old account still holds them). Log in
   as the new Admin, reset the old account's password or give it back the Admin role, and deactivate
   whichever account is no longer needed.

Record every database change made this way (who, when, why), because it bypasses the audit log.
