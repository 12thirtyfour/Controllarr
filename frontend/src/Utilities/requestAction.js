import createAjaxRequest from './createAjaxRequest';

function flattenProviderData(providerData) {
  if (!providerData) {
    return {};
  }

  return Object.keys(providerData).reduce((acc, key) => {
    const property = providerData[key];

    if (key === 'fields') {
      acc[key] = property;
    } else if (property && typeof property === 'object' && 'value' in property) {
      acc[key] = property.value;
    } else {
      acc[key] = property;
    }

    return acc;
  }, {});
}

function requestAction(payload) {
  const {
    provider,
    action,
    providerData,
    queryParams
  } = payload;

  const ajaxOptions = {
    url: `/${provider}/action/${action}`,
    contentType: 'application/json',
    method: 'POST',
    data: JSON.stringify(flattenProviderData(providerData))
  };

  if (queryParams) {
    const searchParams = new URLSearchParams();
    Object.entries(queryParams).forEach(([key, value]) => {
      if (value != null) {
        searchParams.append(key, String(value));
      }
    });
    const queryString = searchParams.toString();

    if (queryString) {
      ajaxOptions.url += `?${queryString}`;
    }
  }

  return createAjaxRequest(ajaxOptions).request;
}

export default requestAction;
