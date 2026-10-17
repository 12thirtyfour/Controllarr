const fs = require('fs');

async function main() {
  const eventName = process.env.EVENT_NAME;
  const token = process.env.GITHUB_TOKEN;
  const apiKey = (process.env.GEMINI_API_KEY || '').trim();
  const owner = process.env.REPO_OWNER;
  const repo = process.env.REPO_NAME;
  const actor = process.env.ACTOR;

  let payload = {};
  if (process.env.GITHUB_EVENT_PATH && fs.existsSync(process.env.GITHUB_EVENT_PATH)) {
    payload = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  }

  const issueNumber = payload.issue?.number || payload.pull_request?.number;
  if (!issueNumber) return;

  // Acknowledge comment events with an eyes reaction
  if (eventName === 'issue_comment' && payload.comment?.id) {
    try {
      await fetch(`https://api.github.com/repos/${owner}/${repo}/issues/comments/${payload.comment.id}/reactions`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Accept': 'application/vnd.github+json',
          'User-Agent': 'controllarr-architect-relay'
        },
        body: JSON.stringify({ content: 'eyes' })
      });
    } catch (e) {
      // Non-critical; continue execution
    }
  }

  // Load architectural source of truth
  let archState = "";
  try {
    archState = fs.readFileSync('ARCHITECT_STATE.md', 'utf8');
  } catch {
    archState = "No ARCHITECT_STATE.md found at repository root.";
  }

  // Format context payload
  let incoming = "";
  if (eventName === 'issues') {
    incoming = `New Issue #${issueNumber}: ${payload.issue?.title}\n\n${payload.issue?.body || ''}`;
  } else if (eventName === 'issue_comment') {
    incoming = `Comment on Issue/PR #${issueNumber} by @${actor}:\n\n${payload.comment?.body || ''}`;
  } else if (eventName === 'pull_request') {
    incoming = `Pull Request #${issueNumber} by @${actor}:\nTitle: ${payload.pull_request?.title}\nBody:${payload.pull_request?.body || ''}`;
  }

  const promptText = `System Instructions:
You are the lead architect and project manager for Controllarr.
Maintain the architectural source of truth based on ARCHITECT_STATE.md.
Give concise, authoritative instructions to Jules (the software engineer).

ARCHITECT_STATE.md:
${archState}

User Input:
${incoming}`;

  // Call Gemini API
  const url = `https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent?key=${apiKey}`;
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
    throw new Error(`Gemini API HTTP ${res.status}:${errText}`);
  }

  const data = await res.json();
  const reply = data.candidates?.[0]?.content?.parts?.[0]?.text || "Architect Review Error: Empty response payload received.";

  // Post comment back to GitHub
  const postRes = await fetch(`https://api.github.com/repos/${owner}/${repo}/issues/${issueNumber}/comments`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Accept': 'application/vnd.github+json',
      'User-Agent': 'controllarr-architect-relay'
    },
    body: JSON.stringify({
      body: `### 🏛️ Architect (Gemini)\n\n${reply}`
    })
  });

  if (!postRes.ok) {
    throw new Error(`Failed to post
