const fs = require('fs');

async function main() {
  const eventName = process.env.EVENT_NAME;
  const payload = JSON.parse(process.env.EVENT_PAYLOAD || '{}');
  const token = process.env.GITHUB_TOKEN;
  const apiKey = (process.env.GEMINI_API_KEY || '').trim();
  const owner = process.env.REPO_OWNER;
  const repo = process.env.REPO_NAME;
  const actor = process.env.ACTOR;

  const issueNumber = payload.issue?.number || payload.pull_request?.number;
  if (!issueNumber) {
    console.log("No target issue or pull request number identified. Exiting.");
    return;
  }

  // React with eyes on comments
  if (eventName === 'issue_comment' && payload.comment?.id) {
    try {
      await fetch(`https://api.github.com/repos/${owner}/${repo}/issues/comments/${payload.comment.id}/reactions`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Accept': 'application/vnd.github+json',
          'User-Agent': 'node-fetch'
        },
        body: JSON.stringify({ content: 'eyes' })
      });
    } catch (e) {
      console.warn("Could not add eyes reaction:", e.message);
    }
  }

  // Read architectural state
  let archState = "";
  try {
    archState = fs.readFileSync('ARCHITECT_STATE.md', 'utf8');
  } catch (e) {
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
      throw new Error(`HTTP ${res.status}: ${errText}`);
    }
    const data = await res.json();
    return data.candidates?.[0]?.content?.parts?.[0]?.text;
  }

  let reply = "";
  try {
    try {
      reply = await callGemini('gemini-2.5-pro');
    } catch (primaryErr) {
      console.warn("Primary gemini-2.5-pro failed, falling back to gemini-2.5-flash:", primaryErr.message);
      reply = await callGemini('gemini-2.5-flash');
    }
    if (!reply) {
      reply = "Architect Review Error: Empty response payload.";
    }
  } catch (err) {
    reply = `Architect Review Error: ${err.message}`;
  }

  // Post response comment back to GitHub
  const postRes = await fetch(`https://api.github.com/repos/${owner}/${repo}/issues/${issueNumber}/comments`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Accept': 'application/vnd.github+json',
      'User-Agent': 'node-fetch'
    },
    body: JSON.stringify({
      body: `### 🏛️ Architect (Gemini)\n\n${reply}`
    })
  });

  if (!postRes.ok) {
    console.error("Failed to post comment to issue:", await postRes.text());
  }
}

main().catch(err => {
  console.error("Relay fatal error:", err);
  process.exit(1);
});
