## ADDED Requirements

### Requirement: Screenshot blobs are isolated per project

A stored screenshot blob SHALL be addressed in a way that includes the owning
project, such that a write performed on behalf of one project can never create,
overwrite, or collide with a blob belonging to another project or with any other
object the platform stores in the same backing store. Retrieval of a screenshot
SHALL continue to be gated by the requesting context's access to the specific
report the screenshot belongs to; the per-project addressing is an additional
layer, not a replacement for that check.

#### Scenario: One project cannot overwrite another project's screenshot

- **WHEN** an upload for project A supplies a screenshot whose identifier equals one already stored for project B
- **THEN** project B's stored screenshot is unchanged and project A's screenshot is stored separately under project A

#### Scenario: Retrieval still requires access to the owning report

- **WHEN** a caller requests a screenshot blob for a report they are not entitled to view
- **THEN** the request is refused regardless of whether the screenshot identifier is known

#### Scenario: The purge removes a project's blobs under its own addressing

- **WHEN** the retention purge deletes evidence for a project
- **THEN** it deletes that project's screenshot blobs and no blob belonging to another project
