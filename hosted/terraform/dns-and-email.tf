# company-and-domain-launch: the registered domain (releasetwin.com), and the SES domain identity
# that SesInvitationEmailSender delivers org invitations through.
#
# The Route 53 hosted zone is *looked up*, not managed here — Route 53 Domains auto-creates it at
# registration, and declaring an aws_route53_zone for the same name would silently create a second
# zone with different nameservers that the domain isn't delegated to. Same philosophy as
# alerting.tf treating the auto-created Lambda log group as a name string rather than importing it.
#
# Everything is gated on `domain_name` (the DOMAIN_NAME repo var). Empty ⇒ no zone lookup, no SES
# identity, no records. SesInvitationEmailSender also stays unbound while notifications_from_address
# is empty (Program.cs), so an unset domain is a complete no-op — invitations are log-only with the
# accept link returned in the API response.
#
# An SES domain identity is an account+region singleton for a given domain: only ONE terraform
# stack should set `domain_name`. Today that is the auto-deploy stack (table_prefix
# = releasetwin-dev-). A future dedicated prod stack would take it over — go-public-sequence
# records that migration note. `table_prefix` deliberately does not touch any name here.

variable "domain_name" {
  description = "Registered apex domain (e.g. releasetwin.com). Empty ⇒ no Route 53 / SES resources and invitation email stays log-only. Supplied from the DOMAIN_NAME repo variable by deploy-hosted.yml."
  type        = string
  default     = ""
}

variable "notifications_from_address" {
  description = "company-and-domain-launch: From address for transactional email (org invitations). Must be on domain_name's SES identity, e.g. no-reply@releasetwin.com. Empty ⇒ SesInvitationEmailSender is not bound; invitations are log-only with the accept link in the API response. Supplied from the NOTIFICATIONS_FROM_ADDRESS repo variable."
  type        = string
  default     = ""

  validation {
    condition     = var.notifications_from_address == "" || can(regex("@", var.notifications_from_address))
    error_message = "notifications_from_address must be an email address or empty."
  }
}

variable "enable_google_workspace_email" {
  description = "company-and-domain-launch §2: when true (and domain_name set), publish the Google Workspace MX + apex SPF for the company mailbox (hello@ / security@ / billing@ / legal@ ...). Flip after the Workspace account exists. Supplied from the ENABLE_GOOGLE_WORKSPACE_EMAIL repo variable."
  type        = bool
  default     = false
}

variable "google_workspace_dkim" {
  description = "The DKIM TXT value Google generates in Admin console → Apps → Gmail → Authenticate email (2048-bit). Empty ⇒ the google._domainkey record is not published yet. Supplied from the GOOGLE_WORKSPACE_DKIM repo variable."
  type        = string
  default     = ""
}

variable "google_site_verification" {
  description = "Optional google-site-verification token for the apex TXT record, if Workspace/Search Console asks for one (many setups verify via the MX records or an existing Search Console property instead). Empty ⇒ apex TXT is SPF only."
  type        = string
  default     = ""
}

locals {
  domain_enabled = var.domain_name != ""
  gws_enabled    = local.domain_enabled && var.enable_google_workspace_email
}

data "aws_route53_zone" "main" {
  count = local.domain_enabled ? 1 : 0
  name  = "${var.domain_name}."
}

# --- SES domain identity + Easy DKIM ---------------------------------------------------------------

resource "aws_ses_domain_identity" "main" {
  count  = local.domain_enabled ? 1 : 0
  domain = var.domain_name
}

# Verification TXT. With Easy DKIM below SES also verifies the domain off the DKIM CNAMEs, but the
# _amazonses record is harmless and covers the plain path too. No aws_ses_domain_identity_
# verification resource: it polls until DNS propagates, which would hang/fail `terraform apply` in
# CI — SES completes verification asynchronously once these records resolve.
resource "aws_route53_record" "ses_verification" {
  count   = local.domain_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = "_amazonses.${var.domain_name}"
  type    = "TXT"
  ttl     = 1800
  records = [aws_ses_domain_identity.main[0].verification_token]
}

resource "aws_ses_domain_dkim" "main" {
  count  = local.domain_enabled ? 1 : 0
  domain = aws_ses_domain_identity.main[0].domain
}

resource "aws_route53_record" "ses_dkim" {
  count   = local.domain_enabled ? 3 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = "${aws_ses_domain_dkim.main[0].dkim_tokens[count.index]}._domainkey.${var.domain_name}"
  type    = "CNAME"
  ttl     = 1800
  records = ["${aws_ses_domain_dkim.main[0].dkim_tokens[count.index]}.dkim.amazonses.com"]
}

# --- Custom MAIL FROM: gives an SPF-authenticated envelope sender aligned to the domain, which is
# what DMARC (below) checks. Without it the envelope sender is amazonses.com and DMARC can only pass
# on DKIM alignment.
resource "aws_ses_domain_mail_from" "main" {
  count            = local.domain_enabled ? 1 : 0
  domain           = aws_ses_domain_identity.main[0].domain
  mail_from_domain = "mail.${var.domain_name}"
}

resource "aws_route53_record" "ses_mail_from_mx" {
  count   = local.domain_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = aws_ses_domain_mail_from.main[0].mail_from_domain
  type    = "MX"
  ttl     = 1800
  records = ["10 feedback-smtp.${var.region}.amazonses.com"]
}

resource "aws_route53_record" "ses_mail_from_spf" {
  count   = local.domain_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = aws_ses_domain_mail_from.main[0].mail_from_domain
  type    = "TXT"
  ttl     = 1800
  records = ["v=spf1 include:amazonses.com -all"]
}

# --- DMARC: monitor-only (p=none) to start, so a misaligned sender shows up in the aggregate
# reports before anything is quarantined. Tighten to p=quarantine once reports are clean.
resource "aws_route53_record" "dmarc" {
  count   = local.domain_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = "_dmarc.${var.domain_name}"
  type    = "TXT"
  ttl     = 1800
  records = ["v=DMARC1; p=none; rua=mailto:security@${var.domain_name}"]
}

# --- Company mailbox: Google Workspace on the apex ------------------------------------------------
#
# Separate from SES: SES sends *transactional* mail from mail.releasetwin.com (a subdomain, its own
# SPF above). Google Workspace handles person-to-person mail from @releasetwin.com (the apex). The
# two do not collide — apex SPF authorises Google, the MAIL FROM subdomain SPF authorises SES, and
# the one _dmarc record covers both.
#
# Gated on `enable_google_workspace_email` (flip after the Workspace account is created). The
# google._domainkey record is additionally gated on `google_workspace_dkim` being set — generate
# the key in Admin console → Apps → Google Workspace → Gmail → Authenticate email, then put the
# TXT value in the GOOGLE_WORKSPACE_DKIM repo variable.

resource "aws_route53_record" "gws_mx" {
  count   = local.gws_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = var.domain_name
  type    = "MX"
  ttl     = 3600
  records = ["1 smtp.google.com"]
}

resource "aws_route53_record" "gws_apex_txt" {
  count   = local.gws_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = var.domain_name
  type    = "TXT"
  ttl     = 3600
  # One record set: the SPF plus, optionally, a Google site-verification token. Anything else that
  # needs an apex TXT (a future verification, etc.) must be added to this list, not a second resource.
  records = compact([
    "v=spf1 include:_spf.google.com ~all",
    var.google_site_verification != "" ? "google-site-verification=${var.google_site_verification}" : "",
  ])
}

resource "aws_route53_record" "gws_dkim" {
  count   = local.gws_enabled && var.google_workspace_dkim != "" ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = "google._domainkey.${var.domain_name}"
  type    = "TXT"
  ttl     = 3600
  # Route 53 caps each TXT character-string at 255 bytes; a 2048-bit DKIM value is ~400. Split it
  # into 255-char segments joined by `" "` (a literal close-quote / space / open-quote). The AWS
  # provider adds the outer quotes, so Route 53 stores ONE record whose value is `"seg1" "seg2"` —
  # multiple character-strings a DKIM verifier concatenates.
  records = [join("\" \"", regexall(".{1,255}", var.google_workspace_dkim))]
}

output "ses_domain_identity_arn" {
  value = local.domain_enabled ? aws_ses_domain_identity.main[0].arn : null
}

output "ses_mail_from_domain" {
  value = local.domain_enabled ? aws_ses_domain_mail_from.main[0].mail_from_domain : null
}
