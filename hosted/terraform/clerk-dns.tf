# company-and-domain-launch: CNAME records that connect the production Clerk instance to
# clerk.releasetwin.com — its Frontend API + Account Portal, and the mail/DKIM records Clerk uses
# to send auth emails (verification codes, magic links) from @releasetwin.com.
#
# Values from Clerk dashboard → Production instance → Configure → Domains, captured 2026-09-01.
# The `f7vl3v6j0gvm` segment is this Clerk instance's id — re-check the panel if the instance is
# ever recreated.
#
# Independent of the SES setup in dns-and-email.tf: Clerk's auth email uses its own subdomain
# (clkmail) and DKIM selectors (clk._domainkey / clk2._domainkey); SES invitation email uses
# mail.releasetwin.com and its own token selectors. No collision.
#
# Same `domain_name` gate as the rest — empty ⇒ no records.

locals {
  clerk_cnames = local.domain_enabled ? {
    "clerk"           = "frontend-api.clerk.services"
    "accounts"        = "accounts.clerk.services"
    "clkmail"         = "mail.f7vl3v6j0gvm.clerk.services"
    "clk._domainkey"  = "dkim1.f7vl3v6j0gvm.clerk.services"
    "clk2._domainkey" = "dkim2.f7vl3v6j0gvm.clerk.services"
  } : {}
}

resource "aws_route53_record" "clerk" {
  for_each = local.clerk_cnames
  zone_id  = data.aws_route53_zone.main[0].zone_id
  name     = "${each.key}.${var.domain_name}"
  type     = "CNAME"
  ttl      = 300
  records  = [each.value]
}
