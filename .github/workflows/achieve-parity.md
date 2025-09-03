---
on:
  workflow_dispatch:  # Manual trigger
  schedule:
    - cron: "0 9 * * *"  # Every day at 9 AM UTC

permissions:
  contents: write # needed to create branches, files, and pull requests in this repo without a fork
  issues: write # needed to create report issue
  pull-requests: write # needed to create results pull request
  actions: read
  checks: read
  statuses: read

# Tools - what APIs and tools can the AI use?
tools:
  github:
    allowed: ["*"]
  claude:
    allowed:
      Edit:
      MultiEdit:
      Write:
      NotebookEdit:
      WebFetch:
      WebSearch:
      Bash: ["*"]

# Advanced options (uncomment to use):
# engine: claude  # AI engine (default: claude)
timeout_minutes: 30  # Max runtime (default: 15)
# runs-on: ubuntu-latest  # Runner type (default: ubuntu-latest)

steps:
  - name: Checkout repository
    uses: actions/checkout@v5

  - name: Check if action.yml exists
    id: check_build_steps_file
    run: |
      if [ -f ".github/actions/achieve-parity/build-steps/action.yml" ]; then
        echo "exists=true" >> $GITHUB_OUTPUT
      else
        echo "exists=false" >> $GITHUB_OUTPUT
      fi
    shell: bash
  - name: Build the project
    if: steps.check_build_steps_file.outputs.exists == 'true'
    uses: ./.github/actions/achieve-parity/build-steps
    id: build-steps

---

# achieve-parity

The goal of this workflow to is compare the implementation of [The Update Framework][tuf] in this repository against
implementations in other ecosystems like Rust, Python, Go, etc. Based on the results of that comparison, you should
create a plan to address any discovered gaps by implementing missing functionality, adding missing test cases, 
expanding the set of samples and examples, and other such activities.

## Instructions

1. Perform research and generate a plan

    1. Check if an open issue with title "${{ github.workflow }}: Achieve Parity" exists. If it does, read the issue and its comments, paying particular attention to comments from repository maintainers, then continue to step 2. If not, follow the steps below to create it:
  
    2. do some deep research into the capabilities, testing, validation and documentation of TUF implementations in other languages and ecosystems:
        * how is validation testing done in those implementations? are there 'golden' datasets or exhaustive tests?
        * what capabilities do those implementations have with regards to offline/online support, or support for expanded kinds of signing keys/mechanisms?
        * what kinds of samples and examples do those implementations have to make it easy to understand how to get started as a _consumer_ of that TUF implementation?
        * are there 'canonical' workflows that TUF implementations do as a kind of 'hello world' example?
        * what kind of API and conceptual documentation do those implementations have to make it easy to learn about the details of using TUF?
    
    3. use this research to write an issue with the title "${{ github.workflow}}: Achieve Parity" and then exit the entire workflow.

2. Generate build steps configuration (if not done before).

    1. Check if `.github/actions/achieve-parity/build-steps/action.yml` exists in this repo. Note this path is relative to the current directory (the root of the repo). If this file exists, it will have been run already as part of the GitHub Action you are executing in, so read the file to understand what has already been run and continue to step 3. Otherwise continue to step 2.2.

    2. Check if an open pull request with title "${{ github.workflow }}: Updates to complete configuration" exists in this repo. If it does, add a comment to the pull request saying configuration needs to be completed, then exit the workflow. Otherwise continue to step 2c.

    3. Have a careful think about the CI commands needed to build the project and set up the environment for individual performance development work, assuming one set of build assumptions and one architecture (the one running). Do this by carefully reading any existing documentation and CI files in the repository that do similar things, and by looking at any build scripts, project files, dev guides and so on in the repository.

    4. Create the file `.github/actions/achieve-parity/build-steps/action.yml` as a GitHub Action containing these steps, ensuring that the action.yml file is valid and carefully cross-checking with other CI files and devcontainer configurations in the repo to ensure accuracy and correctness.

    5. Make a pull request for the addition of this file, with title "${{ github.workflow }}: Updates to complete configuration". Explain that adding these files to the repo will make this workflow more reliable and effective. Encourage the maintainer to review the files carefully to ensure they are appropriate for the project. Exit the entire workflow.

3. Goal selection: build an understanding of what to work on and select a part of the 'achieve parity' plan to pursue.

    1. You can now assume the repository is in a state where the steps in `.github/actions/achieve-parity/build-steps/action.yml` have been run and is ready for testing, building, validation, etc. Read this file to understand what has been done.

    2. Read the plan in the issue mentioned earlier, along with comments.

    3. Check any existing open pull requests that are related to achieving parity improvements especially any opened by you starting with title "${{ github.workflow }}".

    4. If you think the plan is inadequate, and needs a refresh, update the planning issue by rewriting the actual body of the issue, ensuring you take into account any comments from maintainers. Add one single comment to the issue saying nothing but the plan has been updated with a one sentence explanation about why. Do not add comments to the issue, just update the body. Then continue to step 3e.

    5. Select a parity improvement goal to pursue from the plan. Ensure that you have a good understanding of the code and the parity gaps before proceeding. Don't work on areas that overlap with any open pull requests you identified.

4. Work towards your selected goal. For the parity improvement goal you selected, do the following:
  
    1. create a new branch
    2. Make the changes to work towards the parity improvement goal you selected. This may involve:
        * Refactoring code
        * Adding new features/APIs
        * Changing data structures
        * Improving engineering practices
        * Adding new documentation
        * Adding entirely new projects, samples, or examples
        * Adding new tests
        * or similar tasks
    3. Ensure the code still works as expected and that any existing relevant tests pass.
    4. After making the changes, if appropriate measure their impact on parity with other TUF implementations.
    Did we fill a functionality gap? Do we have more samples or docs now? Do we support more kinds of keys now? etc.
    5. Apply any automatic code formatting used in the repo
    6. Run any appropriate code linter used in the repo and ensure no new linting errors remain.
    7. While developing, ensure to follow the styles and standards of the rest of the repo - including any AGENTS.md or CLAUDE.md files.
    

[tuf]: https://theupdateframework.io/

@include agentics/shared/include-link.md

@include agentics/shared/job-summary.md

@include agentics/shared/xpia.md

@include agentics/shared/gh-extra-tools.md

@include agentics/shared/tool-refused.md

@include? agentics/achieve-parity.config