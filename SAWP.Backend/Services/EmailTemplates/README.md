# Email templates

One file per email, compiled into `Services.dll` as embedded resources (no file needs to be deployed).

- First line: `Subject: <subject>`, then an empty line, then the HTML body.
- `{{Name}}` placeholders are replaced by `EmailTemplateRenderer`; every value is HTML-encoded. A placeholder
  without a value, or an unknown template, throws, so a mistake shows up in the unit tests
  (`EmailTemplateRendererTests`), not in a user's inbox.
- `_Layout.html` wraps every body (`{{Content}}`); edit the shared header, footer and styles there.
- Texts are Vietnamese (user-facing). The SRS defines no wording for these emails.
- Adding an email: add `<Name>.html`, add the name to `EmailTemplates` in `EmailTemplateRenderer.cs` with its
  values, and add it to the test data.

| Template | Sent by | Values |
| --- | --- | --- |
| `Activation.html` | `AccountLinkService.SendActivationLinkAsync` (BR-63, UC-66 A3) | FullName, Username, Link, Hours |
| `PasswordRecovery.html` | `AccountLinkService.SendRecoveryLinkAsync` (UC-66) | FullName, Link, Minutes |
