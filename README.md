# gather-wakatime-status

A simple application that brings the Intelligent TV Bot in Gather v2 to life by using WakaTime to check whether you're currently coding.

## How to use

### 1. Fork the repository

First, fork this repository to your own GitHub account.

Then, clone **your fork** to your local machine:

```bash
git clone https://github.com/your-username/gather-wakatime-status.git
cd gather-wakatime-status
```

Replace `your-username` with your GitHub username.

### 2. Configure the environment variables

Create a `.env` file in the project root and add the following variables:

```env
WAKATIME_API_KEY=your_wakatime_api_key
GATHER_WEBHOOK_SECRET=your_gather_webhook_secret
GATHER_WEBHOOK_URL=your_gather_webhook_url
```

### 3. Push your changes

After configuring the project, commit and push your changes to your fork:

```bash
git add .
git commit -m "Configure project"
git push
```

Then, go to the **Actions** tab of your GitHub repository.

If everything is configured correctly, the workflow should run automatically and update your status in Gather.

## Update frequency

The status is updated **every 5 minutes**.

Keep in mind that the actual update may take longer due to delays from GitHub Actions and the Gather webhook.

If you want to change the update frequency, edit:

```text
.github/workflows/status.yml
```

Look for:

```yaml
on:
  schedule:
    - cron: '*/5 * * * *'
```

### Cron syntax

Cron expressions follow this format:

```text
┌ minute (0-59)
│ ┌ hour (0-23)
│ │ ┌ day of the month (1-31)
│ │ │ ┌ month (1-12)
│ │ │ │ ┌ day of the week (0-6)
│ │ │ │ │
* * * * *
```

For example:

```yaml
'*/5 * * * *'
```

runs the workflow **every 5 minutes**.

Some other examples:

```yaml
'*/10 * * * *'  # Every 10 minutes
'*/15 * * * *'  # Every 15 minutes
'*/30 * * * *'  # Every 30 minutes
'0 * * * *'     # Every hour
```

> **Note:** GitHub Actions scheduled workflows are not guaranteed to run at the exact scheduled time, so the update may occasionally take longer than the configured interval.


 
