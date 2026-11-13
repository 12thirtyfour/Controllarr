const absUrlRegex = /^(https?:)?\/\//i;

function isRelative(ajaxOptions) {
  return !absUrlRegex.test(ajaxOptions.url);
}

function addRootUrl(ajaxOptions) {
  const apiRoot = window.Controllarr?.apiRoot || '/api/v5';
  ajaxOptions.url = apiRoot + ajaxOptions.url;
}

function addApiKey(ajaxOptions) {
  ajaxOptions.headers = ajaxOptions.headers || {};

  if (window.Controllarr?.apiKey) {
    ajaxOptions.headers['X-Api-Key'] = window.Controllarr.apiKey;
  }
}

function addContentType(ajaxOptions) {
  if (
    ajaxOptions.contentType == null &&
    ajaxOptions.dataType === 'json' &&
    (ajaxOptions.method === 'PUT' || ajaxOptions.method === 'POST' || ajaxOptions.method === 'DELETE')
  ) {
    ajaxOptions.contentType = 'application/json';
  }
}

export default function createAjaxRequest(originalAjaxOptions) {
  const abortController = new AbortController();
  let completed = false;

  const ajaxOptions = { ...originalAjaxOptions };

  if (isRelative(ajaxOptions)) {
    addRootUrl(ajaxOptions);
    addApiKey(ajaxOptions);
    addContentType(ajaxOptions);
  }

  const method = (ajaxOptions.method || 'GET').toUpperCase();
  const headers = { ...(ajaxOptions.headers || {}) };

  if (ajaxOptions.contentType) {
    headers['Content-Type'] = ajaxOptions.contentType;
  }

  let body = ajaxOptions.data;

  if (body != null && typeof body === 'object' && !(body instanceof FormData) && !(body instanceof URLSearchParams)) {
    body = JSON.stringify(body);

    if (!headers['Content-Type']) {
      headers['Content-Type'] = 'application/json';
    }
  }

  const fetchPromise = fetch(ajaxOptions.url, {
    method,
    headers,
    body: method === 'GET' || method === 'HEAD' ? undefined : body,
    signal: abortController.signal
  }).then(async(response) => {
    completed = true;
    const contentType = response.headers.get('content-type') || '';
    let responseData = null;
    let responseText = '';

    try {
      if (contentType.includes('application/json')) {
        responseData = await response.json();
      } else {
        responseText = await response.text();

        try {
          responseData = JSON.parse(responseText);
        } catch {
          responseData = responseText;
        }
      }
    } catch {
      responseData = null;
    }

    if (!response.ok) {
      const error = new Error(`Request failed with status ${response.status}: ${response.statusText}`);
      error.status = response.status;
      error.statusText = response.statusText;
      error.responseJSON = typeof responseData === 'object' ? responseData : undefined;
      error.responseText = responseText || (typeof responseData === 'string' ? responseData : '');
      error.aborted = abortController.signal.aborted;
      throw error;
    }

    return responseData;
  }).catch((err) => {
    completed = true;

    if (abortController.signal.aborted) {
      err.aborted = true;
    }

    throw err;
  });

  const enhancePromise = (p) => {
    p.done = function(callback) {
      const next = this.then((data) => {
        callback(data);
        return data;
      });
      return enhancePromise(next);
    };

    p.fail = function(onFail) {
      const next = this.catch((error) => {
        onFail(error, error.statusText || 'error', error);
        throw error;
      });
      return enhancePromise(next);
    };

    p.always = function(callback) {
      const next = this.finally(() => {
        callback();
      });
      return enhancePromise(next);
    };

    return p;
  };

  const request = enhancePromise(fetchPromise);

  function abortRequest() {
    if (!completed) {
      abortController.abort();
    }
  }

  return {
    request,
    abortRequest
  };
}
