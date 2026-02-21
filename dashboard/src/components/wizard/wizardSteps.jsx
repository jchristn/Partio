const wizardSteps = [
  {
    title: 'Tenant Configuration',
    navigateTo: '/tenants',
    buttonLabel: 'Go to Tenants',
    icon: (
      <svg width="32" height="32" viewBox="0 0 20 20" fill="var(--primary-color)">
        <path fillRule="evenodd" d="M4 4a2 2 0 012-2h8a2 2 0 012 2v12a1 1 0 110 2h-3a1 1 0 01-1-1v-2a1 1 0 00-1-1H9a1 1 0 00-1 1v2a1 1 0 01-1 1H4a1 1 0 110-2V4zm3 1h2v2H7V5zm2 4H7v2h2V9zm2-4h2v2h-2V5zm2 4h-2v2h2V9z" clipRule="evenodd" />
      </svg>
    ),
    description:
      'Verify that your tenant exists or create a new one. Tenants are isolated workspaces that keep data, users, and configurations separate.',
    tip: 'A default tenant is usually created automatically. Check that it is active before proceeding.',
  },
  {
    title: 'User Management',
    navigateTo: '/users',
    buttonLabel: 'Go to Users',
    icon: (
      <svg width="32" height="32" viewBox="0 0 20 20" fill="var(--primary-color)">
        <path d="M9 6a3 3 0 11-6 0 3 3 0 016 0zM17 6a3 3 0 11-6 0 3 3 0 016 0zM12.93 17c.046-.327.07-.66.07-1a6.97 6.97 0 00-1.5-4.33A5 5 0 0119 16v1h-6.07zM6 11a5 5 0 015 5v1H1v-1a5 5 0 015-5z" />
      </svg>
    ),
    description:
      'Create user accounts and assign roles. Admins can manage all settings; Users can process data and view results.',
    tip: 'You need at least one Admin user per tenant to manage configuration.',
  },
  {
    title: 'API Credentials',
    navigateTo: '/credentials',
    buttonLabel: 'Go to Credentials',
    icon: (
      <svg width="32" height="32" viewBox="0 0 20 20" fill="var(--primary-color)">
        <path fillRule="evenodd" d="M18 8a6 6 0 01-7.743 5.743L10 14l-1 1-1 1H6v2H2v-4l4.257-4.257A6 6 0 1118 8zm-6-4a1 1 0 100 2 2 2 0 012 2 1 1 0 102 0 4 4 0 00-4-4z" clipRule="evenodd" />
      </svg>
    ),
    description:
      'Generate bearer tokens that authenticate your API requests. Each credential is scoped to a specific tenant and user.',
    tip: 'Copy and save the bearer token immediately after creation - it cannot be retrieved later.',
  },
  {
    title: 'Embedding Endpoint',
    navigateTo: '/endpoints/embeddings',
    buttonLabel: 'Go to Embeddings',
    icon: (
      <svg width="32" height="32" viewBox="0 0 20 20" fill="var(--primary-color)">
        <path d="M13 7H7v6h6V7z" />
        <path fillRule="evenodd" d="M7 2a1 1 0 012 0v1h2V2a1 1 0 112 0v1h2a2 2 0 012 2v2h1a1 1 0 110 2h-1v2h1a1 1 0 110 2h-1v2a2 2 0 01-2 2h-2v1a1 1 0 11-2 0v-1H9v1a1 1 0 11-2 0v-1H5a2 2 0 01-2-2v-2H2a1 1 0 110-2h1V9H2a1 1 0 010-2h1V5a2 2 0 012-2h2V2zM5 5h10v10H5V5z" clipRule="evenodd" />
      </svg>
    ),
    description:
      'Configure at least one embedding endpoint to generate vector embeddings from your document chunks. Supports OpenAI, Ollama, and other providers.',
    tip: 'This is required for processing. Make sure your API key and model name are correct.',
  },
  {
    title: 'Completion Endpoint (Optional)',
    navigateTo: '/endpoints/inference',
    buttonLabel: 'Go to Inference',
    icon: (
      <svg width="32" height="32" viewBox="0 0 20 20" fill="var(--primary-color)">
        <path fillRule="evenodd" d="M11.3 1.046A1 1 0 0112 2v5h4a1 1 0 01.82 1.573l-7 10A1 1 0 018 18v-5H4a1 1 0 01-.82-1.573l7-10a1 1 0 011.12-.38z" clipRule="evenodd" />
      </svg>
    ),
    description:
      'Optionally configure a completion/inference endpoint if you want to use LLM-powered summarization on your document chunks.',
    tip: 'This step is optional. You can skip it and add an inference endpoint later.',
  },
  {
    title: 'Test with Process Cells',
    navigateTo: '/process',
    buttonLabel: 'Go to Process Cells',
    icon: (
      <svg width="32" height="32" viewBox="0 0 20 20" fill="var(--primary-color)">
        <path fillRule="evenodd" d="M6 2a2 2 0 00-2 2v12a2 2 0 002 2h8a2 2 0 002-2V7.414A2 2 0 0015.414 6L12 2.586A2 2 0 0010.586 2H6zm5 6a1 1 0 10-2 0v3.586l-1.293-1.293a1 1 0 10-1.414 1.414l3 3a1 1 0 001.414 0l3-3a1 1 0 00-1.414-1.414L11 11.586V8z" clipRule="evenodd" />
      </svg>
    ),
    description:
      'Try out the full pipeline! Enter some text, chunk it into semantic segments, generate embeddings, and optionally run summarization.',
    tip: 'Start with a short paragraph to verify everything works before processing larger documents.',
  },
];

export default wizardSteps;
