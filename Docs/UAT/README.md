# UAT and training

`CIP_UAT_and_Training_Guide.docx`: User Acceptance Testing scripts (77 tests in 14 areas,
with Pass/Fail columns), issue log, training plan per role, sign-in quick reference card and
sign-off page. Version 1.1, 2026-10-15 (adds confirm-it's-you and sign-in alerts), for the Hithaai Roohun 2028 deployment.

To change it, edit `tools/build-uat.js` (the test cases are the `modules` list) and rebuild:

```
npm install docx
node tools/build-uat.js CIP_UAT_and_Training_Guide.docx ../Brand/cip-logo-1280.png
```

In Word, right-click the Contents table → Update Field to fill in page numbers.
Update the tests whenever a feature changes (e.g. when the Dhivehi interface arrives).
