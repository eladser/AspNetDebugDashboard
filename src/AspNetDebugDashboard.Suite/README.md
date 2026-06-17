# AspNetDebugDashboard.Suite

Shared plumbing for the [AspNetDebugDashboard](https://github.com/eladser/AspNetDebugDashboard) tool suite. Each tool (Mailbox, Flags, Jobs, Vitals, and the dashboard) registers a `SuitePanel` describing itself; every tool then renders a common sidebar linking to all installed siblings.

You normally don't reference this directly: it comes in as a dependency of the suite packages. It carries no logic beyond the shared contract.

## License

MIT.
