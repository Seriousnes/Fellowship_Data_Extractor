# Deploying the codex

The codex runs as one Docker container on the Lightsail instance `seriousnes`, behind the Caddy
that already serves Foundry. Caddy terminates TLS for `codex.fellowshipanalyzer.com` and reverse
proxies to the container on loopback port 5280. The container reads the export from a private S3
bucket at startup and keeps it in memory. Browsers fetch the art (`ui/*.png`) through a CloudFront
distribution that serves the same bucket at `cdn.codex.fellowshipanalyzer.com`.

Merging to `master` deploys. The workflow `.github/workflows/deploy.yml` builds the image, pushes it
to the GitHub container registry, and starts it on the instance over SSH. What stays on your machine
is the AWS provisioning and the extractor run that publishes the export and the art to the bucket.

## First-time provisioning

`deploy/aws/provision.ps1` creates the AWS resources with CloudFormation and hands you the
credentials the server and the extractor use. Run it once with account credentials:

```powershell
pwsh ./deploy/aws/provision.ps1
```

The script looks up the hosted zone id for `fellowshipanalyzer.com` and the instance's IPv4 and
IPv6 addresses. It deploys `deploy/aws/fellowship-codex-certificate.yaml` to `us-east-1`, the region
CloudFront reads certificates from, and takes the certificate ARN from its output. It then validates
and deploys the template `deploy/aws/fellowship-codex.yaml` to `ap-southeast-2` with that ARN, and
reads its outputs. It creates one access key for `fellowship-codex-server` and writes
`deploy/codex.env` with the bucket name, `https://cdn.codex.fellowshipanalyzer.com` as
`S3__PublicUrl`, the region, and that key pair. When the server user already holds the key that
`deploy/codex.env` carries, the script keeps that key and rewrites the file around it, so a re-run
applies a template change to the environment the server reads. It creates
one access key for `fellowship-codex-extractor` and writes it into a local AWS CLI profile named
`fellowship-codex-extractor`. It also saves each key pair as `<user>_accessKeys.csv`, the format the
IAM console downloads, under `$env:USERPROFILE\Documents\AWS\Codex`; pass `-CredentialsDir` to save them elsewhere.

The stack creates:

- The private bucket `fellowship-codex` in `ap-southeast-2`, with public access blocked and object
  ownership enforced.
- A CloudFront distribution that reads the bucket through an origin access control, answers to
  `cdn.codex.fellowshipanalyzer.com` under the ACM certificate, redirects HTTP to HTTPS, compresses
  responses, serves IPv6, HTTP/2 and HTTP/3, and uses `PriceClass_All` so Sydney viewers reach a
  nearby edge. A response headers policy attaches `Access-Control-Allow-Origin: *` at the edge.
- The bucket policy that lets the CloudFront service principal read the art (`ui/*`) for that distribution.
- The IAM users `fellowship-codex-server` (read the bucket) and `fellowship-codex-extractor` (read
  and write the bucket).
- The Route 53 `A` and `AAAA` records that point `codex.fellowshipanalyzer.com` at the instance.
- The Route 53 alias `A` and `AAAA` records that point `cdn.codex.fellowshipanalyzer.com` at the
  distribution.

The certificate stack `fellowship-codex-certificate` in `us-east-1` creates the ACM certificate for
`cdn.codex.fellowshipanalyzer.com` and the DNS records that validate it, which CloudFormation writes
into the same hosted zone. The first deployment waits for ACM to see those records, which takes a few
minutes.

Each user holds one access key. To rotate a key, delete the existing one in IAM and run
`provision.ps1` again; the script names the key to delete and writes a fresh one.

`deploy/codex.env` and the CSV files hold secrets, and `deploy/codex.env` is gitignored. Keep them on your machine.

## First-time server setup

Copy `deploy/server/setup.sh` and `deploy/caddy/codex.caddy` into one directory on the instance,
then run the script:

```powershell
scp -i $env:USERPROFILE\.ssh\seriousnes-lightsail deploy/server/setup.sh deploy/caddy/codex.caddy ubuntu@foundry.seriousnes.com:~/
ssh -i $env:USERPROFILE\.ssh\seriousnes-lightsail ubuntu@foundry.seriousnes.com "chmod +x setup.sh && ./setup.sh"
```

`setup.sh` installs Docker Engine and the Compose plugin from Docker's apt repository for Ubuntu
noble, adds `ubuntu` to the `docker` group, writes `/etc/docker/daemon.json` with json-file log
rotation, creates `/opt/codex` owned by `ubuntu`, installs the Caddy site file at
`/etc/caddy/sites/codex.caddy`, and validates and reloads Caddy. The group change takes effect on
your next SSH session.

## DNS

The stack creates the `A` and `AAAA` records for `codex.fellowshipanalyzer.com`. They point at the
instance's static IPv4 and IPv6 addresses. Caddy issues the certificate within seconds of the first
reload.

It also creates the alias `A` and `AAAA` records for `cdn.codex.fellowshipanalyzer.com`, which point
at the distribution and carry no TTL of their own. CloudFront answers them under the ACM certificate
the `us-east-1` stack holds, and ACM renews that certificate on its own while the validation records
stay in the zone.

## Reading the codex from another site

Both halves answer any origin, so a page served from another host reads them from the browser. The
Blazor WebAssembly app at `https://www.fellowshipanalyzer.com` runs on the viewer's machine, and its
requests carry that page's origin rather than the codex's.

The art comes from the CloudFront response headers policy `fellowship-codex-art-headers`, which attaches
`Access-Control-Allow-Origin: *` at the edge. The bucket stays private behind the origin access
control and never sees the `Origin` header.

The records come from the CORS policy `origins`, which `Program.cs` requires on the `/api` group and
on nothing else. It allows any origin, any header, and `GET`, `HEAD` and `OPTIONS` without
credentials. `*` carries no `Vary: Origin`, so the output cache in `Serving.cs` holds one entry per
route whatever origin asked for it.

Confirm both after a deployment:

```powershell
curl.exe -sI -H 'Origin: https://www.fellowshipanalyzer.com' https://cdn.codex.fellowshipanalyzer.com/ui/T_Icon_Armor_Cloth_Chest_01.png
curl.exe -sI -H 'Origin: https://www.fellowshipanalyzer.com' https://codex.fellowshipanalyzer.com/api/item/1
```

Each response carries `access-control-allow-origin: *`. A `200` without that header is the failure
that reads as success in the network tab.

## Publishing the export

Publish the export and art to the bucket from your machine with the extractor profile:

```powershell
$env:AWS_PROFILE = 'fellowship-codex-extractor'
dotnet run --project src/FellowshipExtractor -- --bucket fellowship-codex
```

The extractor writes `export-<build>/data/entities.jsonl`, `export-<build>/data/settings.json`, and
`ui/<texture>.png` to the bucket. It sets `Cache-Control: no-cache` and an MD5 on each art object and
rewrites an art key only when its bytes change.

## Tests on a pull request

`.github/workflows/tests.yml` runs `src/Fellowship.Codex.Tests` on every pull request, and on every
push to `master`. The tracked `output/export-*/data` gives it the documents; the art it draws comes
from the bucket, read with `aws s3 sync` into the newest export folder's `ui/` and held in the
Actions cache under the build id. The cache is what the `master` runs are for: a run restores caches
written by its own ref, its base ref, or the default branch, so without a run on `master` no pull
request could ever read another's download. With one, only the first run after a new export pays for
the 335 MB.

`src/FellowshipExtractor.Tests` stays a local run. It mounts the game paks through `Export.Provider`,
which needs the Steam install no hosted runner has.

The deploy workflow does not run the tests. It assumes what merged to `master` passed them.

## Deploying

Merging to `master` runs `.github/workflows/deploy.yml`. The job tags the image with the short git
sha, builds it for `linux/amd64` with the Actions layer cache, and pushes it to
`ghcr.io/seriousnes/fellowship-codex`. It writes `/opt/codex/codex.env` from the repository secrets
and copies `deploy/compose.yaml` beside it. On the instance it signs in to the registry with the
job's own `GITHUB_TOKEN`, pulls the tag, signs out, and runs `deploy/server/start.sh`, which writes
`/opt/codex/.env` with `CODEX_TAG=<sha>` and runs `docker compose up -d --remove-orphans`. It then
waits for `https://codex.fellowshipanalyzer.com/health` to answer 200 from the runner, printing the
container logs and failing the job if it does not, and runs `deploy/server/prune.sh` to remove
server images older than the newest three, keeping the tag it just deployed.

The registry credential is the job's own token, which expires when the job ends, so no long lived
registry credential rests on the instance. The `deploy-codex` concurrency group holds a second merge
until the first finishes.

The workflow reads these repository secrets:

| Secret | Holds |
| --- | --- |
| `SSH_PRIVATE_KEY` | The private half of the key `ubuntu@foundry.seriousnes.com` accepts. |
| `SSH_KNOWN_HOSTS` | The instance's host key, so the runner verifies what it connects to. |
| `S3_BUCKET` | The bucket the codex reads, written as `S3__Bucket`. |
| `S3_PUBLIC_URL` | `https://cdn.codex.fellowshipanalyzer.com`, written as `S3__PublicUrl`. |
| `AWS_REGION` | The bucket's region. |
| `AWS_ACCESS_KEY_ID` | The `fellowship-codex-server` key id. |
| `AWS_SECRET_ACCESS_KEY` | That key's secret. |

`provision.ps1` writes the same five S3 and AWS values into `deploy/codex.env`, which is where the
secret values come from. After a key rotation, set `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`
again from that file:

```powershell
gh secret set AWS_ACCESS_KEY_ID --body (Select-String '^AWS_ACCESS_KEY_ID=' deploy/codex.env).Line.Split('=', 2)[1]
```

## Rolling back

Run the same workflow from the Actions tab with **Run workflow**, giving the short sha of an image
already in the registry as the `tag` input. The job skips the build and the push, pulls that tag, and
restarts the container against it. List the tags the registry holds with:

```powershell
gh api /user/packages/container/fellowship-codex/versions --jq '.[].metadata.container.tags[]'
```

## Reading logs

Read the container logs on the server:

```bash
docker logs -f fellowship-codex
cd /opt/codex && docker compose logs -f
```

Docker rotates each container's logs at 10 MB and keeps three files.

## Pricing

The distribution runs on pay-as-you-go pricing. CloudFormation creates it on pay-as-you-go, so it
needs no subscription step. The CloudFront always-free tier covers its usage, and this tier does not
expire.

Each month the always-free tier includes:

- 1 TB of data transfer out to the internet.
- 10,000,000 HTTP and HTTPS requests.
- 2,000,000 CloudFront Function invocations.
- Free SSL certificates.

The distribution serves the art, which is a few megabytes of PNG files, so its traffic stays within
these amounts. CloudFront bills usage above an allowance at the standard pay-as-you-go rate for that
resource.

The distribution keeps `PriceClass_All`, so Sydney viewers reach a nearby edge. The distribution
serves the art on `cdn.codex.fellowshipanalyzer.com` under an ACM certificate, which ACM issues,
renews and serves through SNI at no charge.

## Invalidating CloudFront

The cache policy sets a minimum TTL of 0, so CloudFront revalidates each art request against S3 and
serves the new bytes as soon as the extractor rewrites a texture. Invalidate the edge to purge it
after you change the cache policy:

```powershell
aws cloudfront create-invalidation --distribution-id <DistributionId> --paths "/ui/*"
```

That invalidation counts as one path. CloudFront gives you 1,000 invalidation paths each month at no
charge, then charges $0.005 for each path beyond that, so it stays within the monthly allowance.

Read `<DistributionId>` from the stack:

```powershell
aws cloudformation describe-stacks --stack-name fellowship-codex --region ap-southeast-2 --query "Stacks[0].Outputs[?OutputKey=='DistributionId'].OutputValue" --output text
```

## Memory sizing

The instance carries 911 MB of RAM and a 2 GB swapfile. Foundry holds about 279 MB resident. The
codex keeps the newest build parsed and every older build's raw lines in memory, for a working set
of 150 to 300 MB. `compose.yaml` sets `mem_limit` and `memswap_limit` both to 512 MB, so 512 MB is
the container's combined ceiling of RAM and swap. That sits above the 300 MB high mark with room for
garbage collection, and leaves Foundry and the system their share of the physical memory and the
swapfile. `DOTNET_gcServer=0` selects the workstation garbage collector, which keeps the heap smaller
on this shared box.

## Rebuilding the instance

The AWS stack and the DNS records survive an instance rebuild. After the replacement instance is up
and the static IP is reattached, restore the codex:

1. Copy `deploy/server/setup.sh` and `deploy/caddy/codex.caddy` to the instance and run `setup.sh`
   to reinstall Docker, `/opt/codex`, and the Caddy site.
2. Run the deploy workflow from the Actions tab to pull and start the container. It writes
   `/opt/codex/codex.env` from the secrets, whose keys still work.

The records point at the instance IP. When the replacement takes a new IP, run `provision.ps1` again
to update the `A` and `AAAA` records.
