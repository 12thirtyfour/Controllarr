const fs = require('fs');

async function main() {
  console.log("=== Architect Relay Starting ===");

  const eventName = process.env.EVENT_NAME;
  const token = process.env.GITHUB_TOKEN;
  const apiKey = (process.env.GEMINI_API_KEY || '').trim();
  const owner = process.env.REPO_OWNER;
  const repo = process.env.REPO_NAME;
  const actor = process.env.ACTOR;

  console.log(`Event: ${eventName} | Owner: ${owner} | Repo: ${repo} | Actor: ${actor}`);
  console.log(`GEMINI_API_KEY length: ${apiKey.length}`);
  console.log(`GITHUB_TOKEN present: ${!!token}`);

  let payload = {};
  if (process.env.GITHUB_EVENT_PATH && fs.existsSync(process.env.GITHUB_EVENT_PATH)) {
    payload = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  } else {
    console.warn("GITHUB_EVENT_PATH not found or invalid.");
  }

  const issueNumber = payload.issue?.number || payload.pull_request?.number;
  console.log(`Identified Target Issue/PR Number: ${issueNumber}`);

  if (!issueNumber) {
    console.error("FATAL: No issue or pull request number identified from payload. Exiting.");
    process.exit(1);
  }

  // React with eyes
  if (eventName === 'issue_comment' && payload.comment?.id) {
    try {
      console.log(`Adding eyes reaction to comment ID: ${payload.comment.id}`);
      const rxRes = await fetch(`https://api.github.com/repos/${owner}/${repo}/issues/comments/${payload.comment.id}/reactions`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Accept': 'application/vnd.github+json',
          'User-Agent': 'controllarr-architect-relay'
        },
        body: JSON.stringify({ content: 'eyes' })
      });
      console.log(`Reaction response status: ${rxRes.status}`);
    } catch (e) {
      console.warn("Failed to post reaction:", e.message);
    }
  }

  // Read architectural state
  let archState = "";
  try {
    archState = fs.readFileSync('ARCHITECT_STATE.md', 'utf8');
    console.log("Successfully loaded ARCHITECT_STATE.md");
  } catch (e) {
    console.warn("Could not read ARCHITECT_STATE.md, continuing with fallback.");
    archState = "No ARCHITECT_STATE.md found at repository root.";
  }

  // Format context
  let incoming = "";
  if (eventName === 'issues') {
    incoming = `New Issue #${issueNumber}: ${payload.issue?.title}\n\n${payload.issue?.body || ''}`;
  } else if (eventName === 'issue_comment') {
    incoming = `Comment on Issue/PR #${issueNumber} by @${actor}:\n\n${payload.comment?.body || ''}`;
  } else if (eventName === 'pull_request') {
    incoming = `Pull Request #${issueNumber} by @${actor}:\nTitle: ${payload.pull_request?.title}\nBody: ${payload.pull_request?.body || ''}`;
  }

  const promptText = `System Instructions:
You are the lead architect and project manager for Controllarr.
Maintain the architectural source of truth based on ARCHITECT_STATE.md.
Give concise, authoritative instructions to Jules (the software engineer).

ARCHITECT_STATE.md:
${archState}

User Input:
${incoming}`;

  async function callGemini(modelName) {
    console.log(`Querying Gemini API using model: ${modelName}...`);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${modelName}:generateContent?key=${apiKey}`;
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        contents: [{
          role: 'user',
          parts: [{ text: promptText }]
        }]
      })
    });

    if (!res.ok) {
      const errText = await res.text();
      throw new Error(`Gemini API HTTP ${res.status}: ${errText}`);
    }

    const data = await res.json();
    return data.candidates?.[0]?.content?.parts?.[0]?.text;
  }

  let reply = "";
  try {
    try {
      reply = await callGemini('gemini-3.1-pro-preview');
    } catch (primaryErr) {
      console.warn("Primary gemini-3.1-pro-preview failed:", primaryErr.message);
      console.log("Falling back to gemini-3.8-flash...");
      reply = await callGemini('gemini-3.8-flash');
    }

    if (!reply) {
      reply = "Architect Review Error: Empty response payload received from Gemini.";
    }
  } catch (err) {
    console.error("Gemini invocation failed entirely:", err.message);
    reply = `Architect Review Error: ${err.message}`;
  }

  console.log(`Posting response comment to Issue #${issueNumber}...`);
  const commentUrl = `https://api.github.com/repos/${owner}/${repo}/issues/${issueNumber}/comments`;
  const postRes = await fetch(commentUrl, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Accept': 'application/vnd.github+json',
      'User-Agent': 'controllarr-architect-relay'
    },
