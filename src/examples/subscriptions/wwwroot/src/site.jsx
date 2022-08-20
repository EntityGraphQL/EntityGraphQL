import React, { useEffect } from 'react';
import ReactDOM from 'react-dom/client';
import { ApolloClient, InMemoryCache, ApolloProvider, gql, useMutation, split, HttpLink, useSubscription } from '@apollo/client';
import { getMainDefinition } from '@apollo/client/utilities';
import { GraphQLWsLink } from "@apollo/client/link/subscriptions";
import { createClient } from "graphql-ws";

const wsLink = new GraphQLWsLink(createClient({
    url: 'wss://localhost:7161/subscriptions',
}));
const httpLink = new HttpLink({
    uri: 'https://localhost:7161/graphql'
});

const splitLink = split(
    ({ query }) => {
        const definition = getMainDefinition(query);
        return (
            definition.kind === 'OperationDefinition' &&
            definition.operation === 'subscription'
        );
    },
    wsLink,
    httpLink,
);

const client = new ApolloClient({
    link: splitLink,
    cache: new InMemoryCache(),
});

const ChatBox = (props) => {
    const [user, setUser] = React.useState(props.user)
    const [message, setMessage] = React.useState('')
    const [postMessage] = useMutation(gql`mutation PostMessage($user: String!, $message: String!) {
        postMessage(user: $user, message: $message) { id }
    }`)

    return <div className="d-flex flex-column flex-fill p-2">
        <div className="mb-3">
            <label className="form-label">User</label>
            <input type="text" className="form-control" value={user} onChange={e => setUser(e.target.value)} />
        </div>
        <div className="mb-3">
            <label className="form-label">Message</label>
            <textarea className="form-control" rows="3" value={message} onChange={e => setMessage(e.target.value)}></textarea>
        </div>
        <button className="btn btn-primary" onClick={() => {
            postMessage({ variables: { user, message } })
            setMessage('')
        }}>Send</button>
    </div>
}

const ChatRoom = () => {
    const { data, loading } = useSubscription(gql`subscription ChatRoom {
        onMessage { id user text timestamp }
    }`, {
        shouldResubscribe: true,
    });
    const [chat, setChat] = React.useState([]);

    useEffect(() => {
        if (data && !loading)
            setChat(chat => [...chat, data.onMessage])
    }, [data, loading])

    return <div className="d-flex flex-column flex-fill p-2">
        <div className="mb-3">
            <label className="form-label">Chat Room</label>
            <div className='d-flex flex-column flex-fill p-2'>
                {chat.map(message => <div key={message.id} className="d-flex flex-column">
                    <div className="d-flex flex-row">
                        <strong>{message.user}</strong>&nbsp;- {new Date(message.timestamp).toLocaleString()}
                    </div>
                    <div key={message.id} className="d-flex flex-row">
                        {message.text}
                    </div>
                </div>)}
            </div>
        </div>
    </div>
}

const App = () => {
    return <div className="d-flex flex-row flex-fill">
        <ChatBox user="Sally" />

        <ChatRoom />

        <ChatBox user="Harry" />
    </div>
}

const domContainer = document.querySelector('#app-root');
const root = ReactDOM.createRoot(domContainer);
root.render(<ApolloProvider client={client}>
    <App />
</ApolloProvider>);