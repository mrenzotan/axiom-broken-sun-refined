---
name: jira-stories-from-game-plan
description: Use when creating Jira epics or user stories from a game design document, assigning existing tickets to epics, or bulk-creating phase stories via Atlassian MCP tools. Applies to sprint ticket management, backlog grooming, and epic linking.
---

# Creating Jira Epics and Stories via Atlassian MCP

## Overview

Creating Jira tickets via MCP requires a strict tool-loading and creation order. Epics must exist before stories. Existing tickets must be discovered before anything is created. Independent operations must be parallelized.

## Step 1: Load Tools and Get cloudId

Always use `ToolSearch` before calling any Atlassian tool — deferred tools fail silently without their schema loaded.

```
ToolSearch: "select:mcp__atlassian__getAccessibleAtlassianResources"
ToolSearch: "select:mcp__atlassian__searchJiraIssuesUsingJql,mcp__atlassian__createJiraIssue,mcp__atlassian__editJiraIssue,mcp__atlassian__getJiraProjectIssueTypesMetadata"
```

Then call `getAccessibleAtlassianResources` to get the `cloudId`. **Never hardcode it — call this every session.**

## Step 2: Discovery (Run Before Creating Anything)

Run these three in **parallel** to capture current state:

```
# Sprint tickets (to assign to an epic)
searchJiraIssuesUsingJql  →  project = DEV AND sprint = "DEV Sprint N"
                              fields: ["summary", "issuetype", "status", "labels", "parent"]

# Backlog tickets (to assign to an epic)
searchJiraIssuesUsingJql  →  project = DEV AND sprint is EMPTY AND issuetype != Epic ORDER BY created ASC

# Available issue types
getJiraProjectIssueTypesMetadata  →  projectIdOrKey: "DEV"
```

This prevents duplicates and tells you what already has an epic assigned.

## Step 3: Create Epics First

Stories reference epics via the `parent` field. Epics must exist before stories.

- Create all needed epics in one parallel message
- Record each returned epic key (e.g. `DEV-43`)
- Create stories using `"parent": {"key": "DEV-43"}`

```
# ✅ Create 3 epics in one message
[createJiraIssue: Epic A]  [createJiraIssue: Epic B]  [createJiraIssue: Epic C]

# ✅ Link 6 existing stories to their epic in one message
[editJiraIssue: DEV-31 → fields: {"parent": {"key": "DEV-43"}}]
[editJiraIssue: DEV-32 → fields: {"parent": {"key": "DEV-43"}}]
...

# ✅ Create 6 new stories under an epic in one message
[createJiraIssue: Story A, parent: DEV-45]
[createJiraIssue: Story B, parent: DEV-45]
...
```

## Description Format

Always pass `contentFormat: "markdown"`. Use this structure for all stories:

```markdown
## Goal

One paragraph — what this story achieves and why.

## Acceptance Criteria

* Each bullet is independently verifiable
* Edge cases and constraints belong here
* Reference dependent tickets: (DEV-37)
* For data tasks: specify asset paths (Assets/Data/Spells/)
* For architecture tasks: state what must NOT exist after this ticket is done
```

## Fields Quick Reference

| Field | Value |
|-------|-------|
| `contentFormat` | `"markdown"` |
| `responseContentFormat` | `"markdown"` |
| `parent` | `{"key": "DEV-##"}` |
| `additional_fields.labels` | `["phase-4-bridge", "unity"]` |
| `issueTypeName` | `"Epic"` / `"Story"` / `"Task"` / `"Subtask"` |

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Creating stories before their epic | Create epics first, record returned keys |
| Sequential calls for independent tickets | Batch all same-level creates/edits in one message |
| Skipping discovery | Always fetch existing tickets first — prevents duplicates |
| Using `customfield_10014` for epic link | Use `parent` field — works in team-managed Jira projects |
| Creating stories with no parent | Always set `parent` when an epic exists for that phase |
| Hardcoding cloudId | Call `getAccessibleAtlassianResources` every session |
