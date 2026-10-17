# UAT and training

`CIP_UAT_and_Training_Guide.docx`: User Acceptance Testing scripts (84 tests in 14 areas,
with Pass/Fail columns), issue log, training plan per role, sign-in quick reference card and
sign-off page, for the Hithaai Roohun 2028 deployment. Version 1.2, 2026-10-17: testing moves
to the training site (tr.shamheed.com); adds sign-ins and lockouts in the Audit Trail, 8-character
codes only, form length limits, the voter-viewing limit and friendly "not found" messages.
(1.1, 2026-10-15: confirm-it's-you and sign-in alerts.)

To change it, edit `tools/build-uat.js` (the test cases are the `modules` list) and rebuild:

```
npm install docx   (or point NODE_PATH at a folder where it is installed)
node tools/build-uat.js CIP_UAT_and_Training_Guide.docx ../Brand/cip-logo-1280.png
```

In Word, right-click the Contents table → Update Field to fill in page numbers.
Update the tests whenever a feature changes (e.g. when the Dhivehi interface arrives).
