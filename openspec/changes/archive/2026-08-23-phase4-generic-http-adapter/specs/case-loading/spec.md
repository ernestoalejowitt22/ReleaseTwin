## ADDED Requirements

### Requirement: Pipeline step parameters are parsed from the case file
A pipeline step in a case file MAY declare a `with:` block of named parameters. The loader SHALL parse that block into the parameters carried by the resulting `PipelineStep`.

#### Scenario: Step parameters load into the pipeline step
- **WHEN** a case file's pipeline step declares a `with:` block containing named values
- **THEN** the loaded `PipelineStep` carries exactly those named values as its parameters

### Requirement: Environment variable interpolation in parameter values
A string parameter value in a case file MAY reference an environment variable using `${VAR_NAME}` syntax. The loader SHALL resolve that reference to the environment variable's value at load time, so real endpoints and credentials never need to be committed to a case file.

#### Scenario: Environment variable reference resolves to its value
- **WHEN** a parameter value contains `${VAR_NAME}` and the environment variable `VAR_NAME` is set
- **THEN** the loaded parameter value has `${VAR_NAME}` replaced with the environment variable's actual value

#### Scenario: Missing environment variable is a clear load-time error
- **WHEN** a parameter value references `${VAR_NAME}` and that environment variable is not set
- **THEN** the loader rejects the case file with an error naming the file and the missing variable, before any case in the batch is executed
