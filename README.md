# Fable.Lit

Fable.Lit is a collection of tools to help you write [Fable](https://fable.io/) apps by embedding HTML code into your F# code with the power of [Lit](https://lit.dev/).

Before doing anything make sure to install the dependencies after cloning the repository by running:

`npm install`

## How to test locally ?

`npm run test`

## How to publish a new version of the package ?

`npm run publish`

## How to work on the documentation ?

1. `cd docs && npm i && npm run docs:watch`
2. Go to [http://localhost:8080/](http://localhost:8080/)

## How to update the documentation ?

Deployment should be done automatically when pushing to `main` branch.

If the CI is broken, you can manually deploy it by running `cd docs && npm run docs:deploy`.
