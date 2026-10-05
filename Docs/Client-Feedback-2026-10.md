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
2. **Influencers — DONE**
   - Influencers page as a **table** with the same search/filters as Voters
     (constituency with codes, island, search by name/ID/phone, paging). Influencers
     stay global, so every constituency and island is offered.
   - New optional **Category** field: MP, Island Council, GM Member, managed in
     Settings → Lists; category filter includes "No category"; clicking a category
     pill filters to it. Migration `AddInfluencerCategories` (table + nullable
     `Influencers.CategoryId`, existing influencers start with no category);
     rollback `dotnet ef database update AddPermissions`. Dev DB backup:
     `HR28Db_before_influencer_categories_2026-10-05.bak`.
3. **Encounters — DONE** — outcome is Meet / Call / Request; new required
   **Response** field Supports (green) / Undecided (yellow) / Does not support (red),
   shown as coloured pills on the Encounters list and the voter profile, with
   outcome and response filters. The encounter type list (Door Visit, Phone Call…)
   is unchanged. Migration `AddEncounterResponse` maps old data: Positive →
   Supports, Negative → Does not support, Undecided and Follow-up Required →
   Undecided, No Contact → no response; outcome = Call for type "Phone Call", Meet
   otherwise. Rollback `dotnet ef database update AddInfluencerCategories` (maps
   back; Follow-up Required returns as Undecided — exact values are in
   `HR28Db_before_encounter_response_2026-10-05.bak`). The API now also validates
   type / outcome / response / date / notes when an encounter is created.
4. **Political party — DONE** — optional party on every voter (empty = "Not known",
   the default for new voters and for all existing voters). Parties are a managed
   list in Settings → Lists, seeded with the 7 parties the Elections Commission listed
   on 2026-10-05 (MDP, PNC, JP, MDA, AP, MNP, PNF). The Voters list opens filtered to
   the party marked "Voters list opens here" (MDP; administrators can change it, so
   another client can use another party) with "All parties", every party and "Not
   known" selectable; an empty result offers "Show all parties". Dashboard count
   tiles, the top-bar search and "everyone at this house" links search all parties.
   Party changes on a voter are audited ("party Not known → MDP"). Migration
   `AddPoliticalParties`; rollback `dotnet ef database update AddEncounterResponse`
   (recorded parties are lost; backup `HR28Db_before_political_parties_2026-10-05.bak`).
5. **User roles — DONE**
   - Administrators create, rename and delete **custom roles** (Settings → Roles &
     rights). Built-in roles can't be renamed or deleted; a role in use can't be deleted.
   - Rights per role for every action: Voters, Encounters, Pledges (view / add / edit /
     delete) and Influencers (view / add / edit / delete / link to voters), shown as one
     grid; the same grid is used for a person's extra rights. Voter-photo rights come
     with step 6. Every API endpoint checks its right; screens hide what isn't allowed.
     New: encounter and pledge delete (permanent, audited); voter delete is now limited
     to the user's areas.
   - **Default voter-profile view per role**: "Full profile" or "Add encounter only".
     Example from the client: set Collector to "Add encounter only" and untick its
     "View encounters" — collectors then open a voter straight on Add encounter, see no
     history and can't delete (the API returns no history either). Not applied
     automatically: on upgrade every built-in role keeps exactly its current access
     (migration `AddRoleRights`; rollback `dotnet ef database update AddPoliticalParties`).
6. **Voter photos — DONE** — optional photo on the voter profile (replaces the initials
   circle). Two granted rights in the "Voter photos" row of the rights grid: View and
   Edit (add / replace / remove); nobody but the Administrator has them until they are
   granted. JPG/PNG up to 2 MB; location and camera details inside the file are removed
   before saving; stored in the database, shown only through the app to people with the
   right and the voter in their areas. Every add, replace and removal is audited.
   Migration `AddVoterPhotos`; rollback `dotnet ef database update AddRoleRights`.

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
