const tourSteps = [
  {
    type: 'modal',
    title: 'Welcome to Partio',
    description:
      'Partio is a multi-tenant platform for summarization, chunking, and embedding generation. ' +
      'It helps you break documents into meaningful chunks, generate vector embeddings, ' +
      'and summarize content using LLMs.\n\n' +
      'This quick tour will walk you through the main areas of the dashboard.',
  },
  {
    target: 'nav-section-admin',
    title: 'Administration',
    description: 'Manage your tenants, users, and API credentials from this section.',
    position: 'right',
  },
  {
    target: 'nav-tenants',
    title: 'Tenants',
    description: 'Tenants are isolated workspaces, each with their own users, configurations, and data.',
    position: 'right',
  },
  {
    target: 'nav-users',
    title: 'Users',
    description: 'Create and manage users within each tenant. Users can have Admin or User roles.',
    position: 'right',
  },
  {
    target: 'nav-credentials',
    title: 'Credentials',
    description: 'Generate and manage bearer tokens used for authenticating API requests.',
    position: 'right',
  },
  {
    target: 'nav-section-endpoints',
    title: 'Endpoints',
    description: 'Configure connections to your AI model providers for embeddings and inference.',
    position: 'right',
  },
  {
    target: 'nav-embeddings',
    title: 'Embedding Endpoints',
    description: 'Set up connections to embedding providers like OpenAI or Ollama to generate vector embeddings.',
    position: 'right',
  },
  {
    target: 'nav-inference',
    title: 'Inference Endpoints',
    description: 'Configure completion model connections used for optional document summarization.',
    position: 'right',
  },
  {
    target: 'nav-section-processing',
    title: 'Processing',
    description: 'Submit data for processing and review the history of past requests.',
    position: 'right',
  },
  {
    target: 'nav-history',
    title: 'Request History',
    description: 'View an audit log of all past processing requests and their results.',
    position: 'right',
  },
  {
    target: 'nav-process',
    title: 'Process Cells',
    description: 'The interactive workspace where you can chunk documents, generate embeddings, and run summarization.',
    position: 'right',
  },
  {
    target: 'topbar-connection',
    title: 'Server Connection',
    description: 'Shows the connected Partio server URL and a status indicator for the connection.',
    position: 'bottom',
  },
  {
    target: 'topbar-role',
    title: 'Your Role',
    description: 'Displays your current role (Admin or User) which determines your permissions.',
    position: 'bottom',
  },
  {
    target: 'topbar-theme',
    title: 'Theme Toggle',
    description: 'Switch between light and dark themes to suit your preference.',
    position: 'bottom',
  },
];

export default tourSteps;
