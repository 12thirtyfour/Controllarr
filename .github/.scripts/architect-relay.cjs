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
