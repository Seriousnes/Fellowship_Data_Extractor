# Deploying the codex

The codex runs as one Docker container on the Lightsail instance `seriousnes`, behind the Caddy
that already serves Foundry. Caddy terminates TLS for `codex.fellowshipanalyzer.com` and reverse
proxies to the container on loopback port 5280. The container reads the export from a private S3
bucket at startup and keeps it in memory. Browsers fetch the art (`ui/*.png`) through a CloudFront
distribution that serves the same bucket over its `*.cloudfront.net` domain.

Deployment is manual from your Windows machine over SSH. The scripts live in `deploy/` and use the
key `~/.ssh/seriousnes-lightsail`.

## First-time provisioning

`deploy/aws/provision.ps1` creates the AWS resources with CloudFormation and hands you the
credentials the server and the extractor use. Run it once with account credentials:

```powershell
pwsh ./deploy/aws/provision.ps1
```

The script looks up the hosted zone id for `fellowshipanalyzer.com` and the instance's IPv4 and
IPv6 addresses, validates and deploys the template `deploy/aws/fellowship-codex.yaml`, and reads its
outputs. It creates one access key for `fellowship-codex-server` and writes `deploy/codex.env` with
the bucket name, the CloudFront domain as `S3__PublicUrl`, the region, and that key pair. It creates
one access key for `fellowship-codex-extractor` and writes it into a local AWS CLI profile named
`fellowship-codex-extractor`. It also saves each key pair as `<user>_accessKeys.csv`, the format the
IAM console downloads, under `$env:USERPROFILE\Documents\AWS\Codex`; pass `-CredentialsDir` to save them elsewhere.

The stack creates:

- The private bucket `fellowship-codex` in `ap-southeast-2`, with public access blocked, object
  ownership enforced, and a CORS rule that allows `GET` and `HEAD` from `https://codex.fellowshipanalyzer.com`.
- A CloudFront distribution that reads the bucket through an origin access control, redirects HTTP
  to HTTPS, compresses responses, serves IPv6, HTTP/2 and HTTP/3, and uses `PriceClass_All` so
  Sydney viewers reach a nearby edge.
- The bucket policy that lets the CloudFront service principal read the art (`ui/*`) for that distribution.
- The IAM users `fellowship-codex-server` (read the bucket) and `fellowship-codex-extractor` (read
  and write the bucket).
- The Route 53 `A` and `AAAA` records that point `codex.fellowshipanalyzer.com` at the instance.

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

## Publishing the export

Publish the export and art to the bucket from your machine with the extractor profile:

```powershell
$env:AWS_PROFILE = 'fellowship-codex-extractor'
dotnet run --project src/FellowshipExtractor -- --bucket fellowship-codex
```

The extractor writes `export-<build>/data/entities.jsonl`, `export-<build>/data/settings.json`, and
`ui/<texture>.png` to the bucket. It sets `Cache-Control: no-cache` and an MD5 on each art object and
rewrites an art key only when its bytes change.

## Deploying

`deploy/deploy.ps1` builds the image, ships it to the server, and starts it:

```powershell
pwsh ./deploy/deploy.ps1
```

The script tags the image with the short git sha, builds it for `linux/amd64`, saves it to a temp
tar, copies the tar to the server, and loads it. It copies `deploy/compose.yaml` and
`deploy/codex.env` to `/opt/codex/`, writes `/opt/codex/.env` with `CODEX_TAG=<sha>`, and runs
`docker compose up -d --remove-orphans`. It waits for `https://codex.fellowshipanalyzer.com/health`
to answer 200, then removes server images older than the newest three, keeping the tag it just
deployed.

## Rolling back

Redeploy an image already on the server by its tag:

```powershell
pwsh ./deploy/deploy.ps1 -Tag <sha>
```

The script skips the build and the image copy, points `CODEX_TAG` at that sha, and restarts the
container. List the tags on the server with:

```bash
ssh -i ~/.ssh/seriousnes-lightsail ubuntu@foundry.seriousnes.com "docker images fellowship-codex"
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
serves the art on the default `*.cloudfront.net` domain, which uses CloudFront's certificate at no
charge.

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
2. Run `deploy/deploy.ps1` to build, ship, and start the container. It copies the current
   `deploy/codex.env`, whose keys still work.

The records point at the instance IP. When the replacement takes a new IP, run `provision.ps1` again
to update the `A` and `AAAA` records.
