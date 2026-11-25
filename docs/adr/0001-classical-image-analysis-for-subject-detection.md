# Subjects are found by classical image analysis, not a model

Auto crop and Auto crop in region must find the Subject from the pixels alone, because a saved screenshot carries no window metadata. We find it with classical image analysis (brightness, edges, rectangles) and use no ML or vision model and no network call, so that the same image always gives the same crop, the app works offline on screenshots that may show confidential data, and the download stays small.

## Considered Options

- **A hosted vision model**: finds more kinds of Subject, but sends screenshots to a third party, needs a key and a connection, and can return a different box each time.
- **A bundled local model**: keeps images on the machine, but adds a large download and a per-platform runtime to a tool that is otherwise a single executable.

## Consequences

Detection will miss some Subjects, dark-theme windows in particular until they are specifically handled. Every detected crop is therefore shown as an editable rectangle, and Classic crop is always available as the fallback.
