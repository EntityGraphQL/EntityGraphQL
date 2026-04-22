(function () {
  const root = ReactDOM.createRoot(document.getElementById('graphiql'));

  const fetcher = async graphQLParams => {
    const response = await fetch('/api/graphql', {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
      },
      body: JSON.stringify(graphQLParams),
      credentials: 'include',
    });

    return response.json();
  };

  const defaultQuery = `query BooksAndAuthors {
  books {
    title
    genre
    publishedYear
    rating
    author {
      name
      country
    }
  }
  authors {
    name
    books {
      title
    }
  }
}`;

  root.render(
    React.createElement(GraphiQL, {
      fetcher,
      defaultQuery,
      defaultEditorToolsVisibility: true,
      headerEditorEnabled: true,
      shouldPersistHeaders: true,
    })
  );
})();
