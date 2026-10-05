# HR28 — client feedback (October 2026) and build plan

Owner decisions recorded 2026-10-05. Build in this order, one step per commit;
state the migration and its rollback before any schema change (CLAUDE.md).

## Owner decisions

- **Political party on voters:** new field, dropdown of parties registered with the
  Elections Commission (elections.gov.mv). Keep the list in a table editable in
  Settings (parties change), not hard-coded. Default for a new voter = **"Not known"**
  (owner decision: not MDP, so no one is wrongly recorded as an MDP member). The
  **party filter** on the Voters list opens pre-set to **MDP**, with "All parties" and
  every other party selectable.
- **Encounters:** the outcome values become **Meet / Call / Request** (owner chose to
  replace the outcome list, not add a separate type), and each encounter also shows a
  response colour: **green = supports, red = does not support, yellow = undecided**.
  Existing outcome values need a data mapping (state it before migrating).
- **Voter photos: allowed** (owner decision, overrides the earlier CLAUDE.md "no photos"
  rule — update that rule when building). Optional, one photo per voter, stored
  securely (not in wwwroot), visible/uploadable only with a granted right
  (e.g. `Voters.Photo.View`, `Voters.Photo.Edit`); every upload/removal audited.

## Steps

1. **Voters quick wins — DONE** (commit 56786d5): status pop-up on the list
   (`PUT api/voters/{id}/status`), "Match house name exactly" tick box, clicking a
   house name filters to that house in the same constituency and island.
2. **Influencers**
   - Influencers page as a **table** with the same search/filters as Voters
     (constituency with codes, island, search by name/ID/phone, paging).
   - New **Category** field: MP, Island Council, GM Member (manageable list), with a
     category filter. Schema change → migration + rollback.
3. **Encounters** — outcomes Meet / Call / Request + support colour (see decisions).
4. **Political party** — field, Settings list, party filter (default MDP), see decisions.
5. **User roles**
   - Administrators create **custom roles**.
   - Rights assignable per role for every action (view / add / edit / delete) on voters,
     voter photos, influencers, encounters, pledges, etc. Build on the existing granted
     rights (`PermissionCatalog`, Settings → Permissions).
   - **Default voter-profile view per role**, chosen by administrators. Example: a
     Collector's default view is "add encounter" only — no encounter history, no
     delete — unless an administrator allows more. Enforce in the API too, not only
     the screen.
6. **Voter photos** — see decisions.

## Client's original comments (verbatim)

Voters
1. add field for political party dropdown selection for registered parties at elections.gov.mv
2. default selection should be maldivian democratic party (MDP). all parties and other parties can also be taken from this filter
3. voters status should be a popup model onclick of status and change it to avaialble statuses
4. Now house names are searching using a "LIKE" in the query. add a tick box to change to "=" label can be exactly matching house name. Example: "Aage" is a house name. so cannot filter it with this house name only because so many "aage" ends like edherimaaAage.
5. upon clicking house name system should automatically filter out all the voters in this house in the constituency and island.
6. optional voter's photo upload function

Influencers
1. must be listed in tabular format with all the filteration just similar to the voter filteration
2. add a field for Influencers Category (Mp, Island Council, GM Member (system should allow filtering in this field also)

Encounters
1. Outcomes change to status: Meet, Call, Request
2. responses to the encounters with different outcomes green for support, red for does not suppport and yellow for undecided.

User Roles
1. Exclusive crud operations selectable for different roles,
2. administrators can create custom roles and assign different roles like add voter's photo, view voters, influencers, pledges, encounters, etc.. the entire CRUD operations rights should be flexibily assigned.
3. add default view for voter profile screen also. administrators should select default view screen upon viewing of voters.
   example: collectors, role can have default view of adding encounters only. no history of encounters or removing or anything unless defined by administrators

## Working notes

- The owner runs HR28 from Visual Studio (multi-startup "New Profile"). Don't start
  the apps on ports 7242/7128 while they run; to test the API, run a build from a
  separate output folder on another port (e.g. 7299).
- Development secrets live in the owner's own User Secrets; anything a Claude session
  writes outside the project folder is not visible to Visual Studio — give the owner a
  script to run instead.
