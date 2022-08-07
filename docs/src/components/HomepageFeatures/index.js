import React from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';
import Link from '@docusaurus/Link';
import CodeBlock from '@theme/CodeBlock';

const FeatureList = [
  {
    title: '1. Define your schema',
    description: (
      <>
      <p>EntityGraphQL was designed to get you up and running quickly. Expose your exsiting object graph (include an EF <code>DbContext</code>) or build you own schema.</p>
      <p>EntityGraphQL can use reflection to generate your schema from your existing object graph - you can use attributes and options to control what is generated. Or you can create one from scratch only adding the types and fields you require.</p>
      <p><Link className="button button--secondary" to="/docs/schema-creation">Learn more about schema creation</Link></p>
      </>
    ),
    code: {
    language: 'csharp',
    code: `public class DemoContext : DbContext
{ 
  public DbSet<Movie> Movies { get; set; }
  public DbSet<Actor> Actors { get; set; }
}

public class Movie
{
  public int Id { get; set; }
  public string Name { get; set; }
  public Genre Genre { get; set; }
}
// all the rest...`}
  },
  {
    title: '2. Register and customize your schema',
    description: (<> <p>EntityGraphQL integrates easily into ASP.NET allowing you to provide the customization you need.</p> <p>You can also use EntityGraphQL directly without ASP.NET (no dependency on EF either) or in a more custom way in your own controller. You can also define multiple schemas that provide different access or functionality.</p> <p><Link className="button button--secondary" to="/docs/schema-creation/#building-a-full-schema">Learn more</Link></p> </>),
    code: {
      language: 'csharp',
      title: 'Startup.cs',
      code:`public void ConfigureServices(IServiceCollection services)
{
  services.AddGraphQLSchema<DemoContext>();
}

public void Configure(IApplicationBuilder app, DemoContext db)
{
  app.UseEndpoints(endpoints =>
  {
    endpoints.MapGraphQL<DemoContext>();
  });
}`
    }
  },
  {
    title: '3. Query your API',
    description: (<> <p>You're now set to query your API. Add <Link to="/docs/schema-creation/mutations">mutations</Link>, connect <Link to="/docs/schema-creation/other-data-sources">other data sources</Link> and <Link to="/docs/authorization">secure</Link> your data.</p> <p>EntityGraphQL provides a rich set of features for you to build out your APIs as you add features.</p> <p><Link className="button button--primary" to="/docs/getting-started">Get started</Link></p> </>),
    code: {
      language: 'graphql',
      code:`query MyQuery {
  movie(id: 19) {
    name
    genre
    actors {
      id
      name
    }
  }
}`
    }
  },
];

function Feature({title, description, code}) {
  return (<div className='row'>
    <div className={clsx('col col--6')}>
      <div className="padding-horiz--md">
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </div>
    <div className={clsx('col col--6')}>
      <div className="padding-horiz--md">
        <CodeBlock title={code.title} language={code.language}>{code.code}</CodeBlock>
      </div>
    </div>
  </div>);
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
      <div className="col">
        {FeatureList.map((props, idx) => (
          <Feature key={idx} {...props} />
        ))}
      </div>
    </div>
  </section>
  );
}
