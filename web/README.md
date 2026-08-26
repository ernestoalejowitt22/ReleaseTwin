This is a [Next.js](https://nextjs.org) project bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app).

## Getting Started

First, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

You can start editing the page by modifying `app/page.tsx`. The page auto-updates as you edit the file.

This project uses [`next/font`](https://nextjs.org/docs/app/building-your-application/optimizing/fonts) to automatically optimize and load [Geist](https://vercel.com/font), a new font family for Vercel.

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## End-to-end tests

`npm run e2e` runs the Cypress suite in `cypress/e2e` against a locally-run hosted API
(`npm run e2e:api`) and a locally-run Next.js dev server (`npm run e2e:web`). Most specs need
`cypress.env.json` (see `cypress.env.json.example`) for Clerk test credentials.

`cypress/e2e/github-connection.cy.ts` additionally drives the real GitHub OAuth flow, which needs a
second OAuth App registered separately from the production one (GitHub OAuth Apps allow exactly one
callback URL each, and the production app's callback points at the deployed Vercel URL, not
localhost) — see `openspec/changes/e2e-github-connection-flow/design.md`. Run it with:

```bash
npm run e2e:github
```

Nothing needs exporting by hand for this one. Both the second OAuth App's Client ID/Secret/Callback
URL (`releasetwin/e2e/github-oauth-app`) and the test account's own password/TOTP secret
(`releasetwin/e2e/github-account`) live in AWS Secrets Manager, fetched at run time using whatever
AWS credentials are already configured in your shell — `scripts/e2e-api-with-github.mjs` fetches the
former and launches the hosted API with it; a Cypress task fetches the latter. Nobody outside those
two fetches ever sees the raw values.

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.
