# Flight Control HOTAS

Start with docs/HOTAS-HANDOFF.md for hardware checks. The checkout baseline is Flight Control 2.

After committing shared fixes in the sibling Flight Control 2 checkout, sync here:

    git fetch flight-control
    git merge flight-control/feature/flight-control-2

Keep HOTAS-specific work on codex/flight-control-hotas.
