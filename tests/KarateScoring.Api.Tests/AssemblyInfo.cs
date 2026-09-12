// ApiFactory isolates each test run's database via the KARATE_SCORING_DB_PATH environment variable —
// process-wide state, so two factories racing to build their host in parallel could point at each
// other's database. Several controllers (Sauvegardes, Admin) also read that variable directly on every
// request rather than through DI, so serializing here is simpler and safer than trying to override it
// per-request. The suite is small enough that this costs nothing noticeable.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
