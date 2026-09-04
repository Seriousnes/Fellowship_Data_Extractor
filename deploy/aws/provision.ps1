param(
  [string]$CredentialsDir = (Join-Path $env:USERPROFILE 'Documents' 'AWS' 'Codex')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$StackName = 'fellowship-codex'
$CertificateStackName = 'fellowship-codex-certificate'
$Region = 'ap-southeast-2'
$CertificateRegion = 'us-east-1'
$InstanceName = 'seriousnes'
$Domain = 'fellowshipanalyzer.com'
$RecordName = 'codex.fellowshipanalyzer.com'
$CdnRecordName = 'cdn.codex.fellowshipanalyzer.com'
$BucketName = 'fellowship-codex'

$ScriptRoot = Split-Path -Parent $PSCommandPath
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot '..' '..')
$TemplatePath = Join-Path $ScriptRoot 'fellowship-codex.yaml'
$CertificateTemplatePath = Join-Path $ScriptRoot 'fellowship-codex-certificate.yaml'
$EnvPath = Join-Path $RepoRoot 'deploy' 'codex.env'
$ExtractorProfile = 'fellowship-codex-extractor'

function Save-AccessKey([string]$User, $Key) {
  New-Item -ItemType Directory -Force -Path $CredentialsDir | Out-Null
  $path = Join-Path $CredentialsDir "${User}_accessKeys.csv"
  Set-Content -Path $path -Value @('Access key ID,Secret access key', "$($Key.AccessKeyId),$($Key.SecretAccessKey)") -Encoding utf8
  Write-Host "Saved $path."
}

function Get-EnvValue([string]$Path, [string]$Key) {
  if (-not (Test-Path $Path)) { return $null }
  foreach ($line in Get-Content -Path $Path) {
    if ($line -match "^$([regex]::Escape($Key))=(.+)$") { return $Matches[1] }
  }
  return $null
}

function Write-ServerEnv([string]$Path, [string]$Bucket, [string]$PublicUrl, [string]$KeyId, [string]$Secret) {
  $lines = @(
    "S3__Bucket=$Bucket"
    "S3__PublicUrl=$PublicUrl"
    "AWS_REGION=$Region"
    "AWS_ACCESS_KEY_ID=$KeyId"
    "AWS_SECRET_ACCESS_KEY=$Secret"
  )
  Set-Content -Path $Path -Value (($lines -join "`n") + "`n") -NoNewline -Encoding ascii
}

$zones = aws route53 list-hosted-zones-by-name --dns-name $Domain --max-items 10 --output json | ConvertFrom-Json
$zone = $zones.HostedZones | Where-Object { $_.Name -eq "$Domain." } | Select-Object -First 1
if (-not $zone) { throw "No Route 53 hosted zone matches $Domain." }
$HostedZoneId = $zone.Id -replace '^/hostedzone/', ''

$instance = (aws lightsail get-instance --instance-name $InstanceName --region $Region --output json | ConvertFrom-Json).instance
$Ipv4 = $instance.publicIpAddress
$Ipv6 = $instance.ipv6Addresses | Select-Object -First 1
if (-not $Ipv4) { throw "Lightsail instance $InstanceName has no public IPv4 address." }
if (-not $Ipv6) { throw "Lightsail instance $InstanceName has no IPv6 address." }

aws cloudformation validate-template --template-body "file://$CertificateTemplatePath" --region $CertificateRegion | Out-Null
aws cloudformation validate-template --template-body "file://$TemplatePath" --region $Region | Out-Null

Write-Host "Deploying $CertificateStackName in $CertificateRegion. ACM validates $CdnRecordName through DNS, which takes a few minutes on the first run."

aws cloudformation deploy `
  --template-file $CertificateTemplatePath `
  --stack-name $CertificateStackName `
  --region $CertificateRegion `
  --no-fail-on-empty-changeset `
  --parameter-overrides `
    "HostedZoneId=$HostedZoneId" `
    "CdnRecordName=$CdnRecordName"

$CertificateArn = (aws cloudformation describe-stacks --stack-name $CertificateStackName --region $CertificateRegion --query "Stacks[0].Outputs[?OutputKey=='CertificateArn'].OutputValue" --output text).Trim()

aws cloudformation deploy `
  --template-file $TemplatePath `
  --stack-name $StackName `
  --region $Region `
  --capabilities CAPABILITY_NAMED_IAM `
  --no-fail-on-empty-changeset `
  --parameter-overrides `
    "BucketName=$BucketName" `
    "HostedZoneId=$HostedZoneId" `
    "RecordName=$RecordName" `
    "CdnRecordName=$CdnRecordName" `
    "CertificateArn=$CertificateArn" `
    "Ipv4=$Ipv4" `
    "Ipv6=$Ipv6"

$outputList = @(aws cloudformation describe-stacks --stack-name $StackName --region $Region --query 'Stacks[0].Outputs' --output json | ConvertFrom-Json)
$outputs = @{}
foreach ($o in $outputList) { $outputs[$o.OutputKey] = $o.OutputValue }

$CdnUrl = $outputs['CdnUrl']
$DistributionDomain = $outputs['DistributionDomainName']
$Bucket = $outputs['BucketName']
$ServerUser = $outputs['ServerUserName']
$ExtractorUser = $outputs['ExtractorUserName']

$skipped = @()

$serverKeys = @(aws iam list-access-keys --user-name $ServerUser --query 'AccessKeyMetadata[].AccessKeyId' --output json | ConvertFrom-Json)
if ($serverKeys.Count -gt 0) {
  $keyId = Get-EnvValue $EnvPath 'AWS_ACCESS_KEY_ID'
  $secret = Get-EnvValue $EnvPath 'AWS_SECRET_ACCESS_KEY'
  if ($keyId -and $secret -and $serverKeys -contains $keyId) {
    Write-ServerEnv $EnvPath $Bucket $CdnUrl $keyId $secret
    Write-Host "Rewrote $EnvPath around the key $keyId that $ServerUser already holds."
  }
  else {
    $skipped += "$ServerUser holds access key $($serverKeys[0]), which $EnvPath does not carry; delete it with 'aws iam delete-access-key --user-name $ServerUser --access-key-id $($serverKeys[0])' then re-run to write a fresh one."
  }
}
else {
  $serverKey = (aws iam create-access-key --user-name $ServerUser --output json | ConvertFrom-Json).AccessKey
  Save-AccessKey $ServerUser $serverKey
  Write-ServerEnv $EnvPath $Bucket $CdnUrl $serverKey.AccessKeyId $serverKey.SecretAccessKey
  Write-Host "Wrote $EnvPath for $ServerUser."
}

$extractorKeys = @(aws iam list-access-keys --user-name $ExtractorUser --query 'AccessKeyMetadata[].AccessKeyId' --output json | ConvertFrom-Json)
if ($extractorKeys.Count -gt 0) {
  $configured = $null
  try { $configured = (aws configure get aws_access_key_id --profile $ExtractorProfile) } catch { }
  if ($configured -and $extractorKeys -contains $configured.Trim()) {
    Write-Host "The AWS CLI profile $ExtractorProfile already carries the key $($configured.Trim()) that $ExtractorUser holds."
  }
  else {
    $skipped += "$ExtractorUser holds access key $($extractorKeys[0]), which the AWS CLI profile $ExtractorProfile does not carry; delete it with 'aws iam delete-access-key --user-name $ExtractorUser --access-key-id $($extractorKeys[0])' then re-run to write a fresh one."
  }
}
else {
  $extractorKey = (aws iam create-access-key --user-name $ExtractorUser --output json | ConvertFrom-Json).AccessKey
  Save-AccessKey $ExtractorUser $extractorKey
  try {
    aws configure set aws_access_key_id $extractorKey.AccessKeyId --profile $ExtractorProfile
    aws configure set aws_secret_access_key $extractorKey.SecretAccessKey --profile $ExtractorProfile
    aws configure set region $Region --profile $ExtractorProfile
  }
  catch {
    throw "Failed to write the AWS CLI profile $ExtractorProfile."
  }
  Write-Host "Configured AWS CLI profile $ExtractorProfile for $ExtractorUser."
}

Write-Host ""
Write-Host "Stack:        $StackName"
Write-Host "Certificate:  $CertificateStackName ($CertificateRegion)"
Write-Host "Bucket:       $Bucket"
Write-Host "Art:          $CdnUrl"
Write-Host "Distribution: https://$DistributionDomain"
Write-Host "Pricing:      pay-as-you-go (CloudFront always-free tier)"
Write-Host "Server user:  $ServerUser"
Write-Host "Extractor:    $ExtractorUser"
Write-Host "Credentials:  $CredentialsDir"

if ($skipped.Count -gt 0) {
  Write-Host ""
  foreach ($s in $skipped) { Write-Host $s }
  throw "One or more users hold an access key this machine does not carry; rotate the named keys and re-run."
}
