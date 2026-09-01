# company-and-domain-launch: points the registered domain at the Vercel deployment of web/ (the
# marketing site + dashboard). Values are from Vercel's per-domain "DNS configuration" panel
# (Vercel → the releasetwin project → Settings → Domains → View DNS configuration), captured
# 2026-09-01:
#   apex  releasetwin.com      A      216.198.79.1
#   www   www.releasetwin.com  CNAME  e608f126eae5edd8.vercel-dns-017.com
# The www target is project-specific — re-check the panel if the Vercel project is ever recreated.
#
# Same `domain_name` gate as dns-and-email.tf: empty ⇒ no records. Vercel owns the apex/www
# canonical redirect (whichever is marked primary in its dashboard); DNS only needs both to resolve.
# Low TTL while the domain is still being set up.

resource "aws_route53_record" "vercel_apex" {
  count   = local.domain_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = var.domain_name
  type    = "A"
  ttl     = 300
  records = ["216.198.79.1"]
}

resource "aws_route53_record" "vercel_www" {
  count   = local.domain_enabled ? 1 : 0
  zone_id = data.aws_route53_zone.main[0].zone_id
  name    = "www.${var.domain_name}"
  type    = "CNAME"
  ttl     = 300
  records = ["e608f126eae5edd8.vercel-dns-017.com"]
}
