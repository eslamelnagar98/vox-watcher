// Talks to GitHub for the web app. Reads work without a token (public repo);
// saving watches and starting checks need a fine-grained token stored on this device.

const API = "https://api.github.com";
const RAW = "https://raw.githubusercontent.com";

export class GitHubError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

/** What went wrong, in words that say how to fix it. */
function explain(status, action, detail) {
  switch (status) {
    case 401: return `GitHub refused the token while trying to ${action}. It may be mistyped or expired: make a new one in Settings.`;
    case 403: return `The token isn't allowed to ${action}. Give it "Contents: Read and write" and "Actions: Read and write" for this repository.`;
    case 404: return `GitHub couldn't find what's needed to ${action}. Check that the token has access to this repository.`;
    case 409:
    case 422: return `watches.json changed somewhere else while you were editing. Reload, then make your change again.`;
    case 0: return `No connection to GitHub while trying to ${action}. Check your internet and try again.`;
    default: return `GitHub answered ${status} while trying to ${action}.${detail ? " " + detail : ""}`;
  }
}

function toBase64(text) {
  const bytes = new TextEncoder().encode(text);
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

export class GitHub {
  constructor({ owner, repo, token = "", fetchImpl = globalThis.fetch.bind(globalThis) }) {
    this.owner = owner;
    this.repo = repo;
    this.token = token;
    this.fetch = fetchImpl;
  }

  get repoUrl() { return `https://github.com/${this.owner}/${this.repo}`; }

  async #request(path, { method = "GET", body, accept = "application/vnd.github+json", action }) {
    const headers = { Accept: accept, "X-GitHub-Api-Version": "2022-11-28" };
    if (this.token) headers.Authorization = `Bearer ${this.token}`;
    if (body) headers["Content-Type"] = "application/json";
    let res;
    try {
      res = await this.fetch(`${API}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined, cache: "no-store" });
    } catch {
      throw new GitHubError(0, explain(0, action));
    }
    if (!res.ok) {
      let detail = "";
      try { detail = (await res.json()).message ?? ""; } catch { /* not JSON */ }
      if (res.status === 403 && /rate limit/i.test(detail)) {
        throw new GitHubError(403, "GitHub's limit for reading without a token is used up for this hour. Add your token in Settings, or wait a bit.");
      }
      throw new GitHubError(res.status, explain(res.status, action, detail));
    }
    return res;
  }

  /** A text file at a branch. Without a token, read through raw.githubusercontent.com (no rate limit). */
  async readText(path, ref) {
    if (this.token) {
      const res = await this.#request(`/repos/${this.owner}/${this.repo}/contents/${path}?ref=${encodeURIComponent(ref)}`,
        { accept: "application/vnd.github.raw+json", action: `read ${path}` });
      return res.text();
    }
    let res;
    try {
      res = await this.fetch(`${RAW}/${this.owner}/${this.repo}/${ref}/${path}?t=${Date.now()}`, { cache: "no-store" });
    } catch {
      throw new GitHubError(0, explain(0, `read ${path}`));
    }
    if (res.status === 404) return null;
    if (!res.ok) throw new GitHubError(res.status, explain(res.status, `read ${path}`));
    return res.text();
  }

  /** A file plus the sha needed to save it back. */
  async readFile(path, ref) {
    const res = await this.#request(`/repos/${this.owner}/${this.repo}/contents/${path}?ref=${encodeURIComponent(ref)}`, { action: `read ${path}` });
    const json = await res.json();
    const bytes = Uint8Array.from(atob(json.content.replace(/\s/g, "")), (c) => c.charCodeAt(0));
    return { text: new TextDecoder().decode(bytes), sha: json.sha };
  }

  async writeFile(path, branch, text, sha, message) {
    const res = await this.#request(`/repos/${this.owner}/${this.repo}/contents/${path}`, {
      method: "PUT",
      body: { message, content: toBase64(text), sha, branch },
      action: `save ${path}`,
    });
    const json = await res.json();
    return { sha: json.content.sha, commitUrl: json.commit.html_url };
  }

  /** Starts the watcher workflow on main, the same as "Run workflow" on GitHub. */
  async runWorkflow(workflow, ref, inputs) {
    await this.#request(`/repos/${this.owner}/${this.repo}/actions/workflows/${workflow}/dispatches`, {
      method: "POST",
      body: { ref, inputs },
      action: "start a check",
    });
  }

  async workflowRuns(workflow, perPage = 40) {
    const res = await this.#request(`/repos/${this.owner}/${this.repo}/actions/workflows/${workflow}/runs?per_page=${perPage}&exclude_pull_requests=true`,
      { action: "read the check history" });
    return (await res.json()).workflow_runs ?? [];
  }

  /** Confirms the token works for this repository. */
  async checkToken() {
    const res = await this.#request(`/repos/${this.owner}/${this.repo}`, { action: "use this repository" });
    return res.json();
  }
}
