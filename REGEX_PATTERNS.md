# Regex Patterns Reference for RegexBased Chunking

The `RegexBased` chunking strategy splits text at boundaries defined by a regular expression you provide in the `RegexPattern` field. Partio uses .NET's `Regex.Split` internally — your pattern defines **where to cut**, not what to keep.

This guide provides ready-to-use patterns for common document formats and use cases, with example inputs and outputs so you can see exactly how each pattern splits content.

---

## How It Works

1. Partio calls `Regex.Split(text, pattern)` to break the input into segments.
2. Empty and whitespace-only segments are discarded.
3. Segments are grouped together until they reach the `FixedTokenCount` token budget.
4. Overlap settings (`OverlapCount` / `OverlapPercentage`) apply between chunks as usual.

**Key concept:** The pattern is a **split delimiter**, not a match extractor. If you want to split *before* a heading, use a lookahead (`(?=...)`) so the heading text stays in the resulting segment.

### Illustrative Example

**Pattern:** `(?=^#{1,3}\s)` (split before Markdown headings)

**Input:**
```
# Introduction
Partio is a chunking platform.

# Architecture
Partio uses a modular design.

# Deployment
Use Docker Compose to deploy.
```

**Segments after split:**
```
Segment 1: "# Introduction\nPartio is a chunking platform."
Segment 2: "# Architecture\nPartio uses a modular design."
Segment 3: "# Deployment\nUse Docker Compose to deploy."
```

Because the pattern uses a lookahead (`(?=...)`), the `#` heading stays attached to its content. Segments are then grouped up to `FixedTokenCount`. If all three segments fit within the token budget, they merge into one chunk; if not, they become separate chunks.

---

## Pattern Syntax

Partio uses **.NET regular expressions** (`System.Text.RegularExpressions`). This is largely compatible with PCRE but has some differences. Key features:

| Feature | Syntax | Example |
|---------|--------|---------|
| Lookahead | `(?=...)` | `(?=^# )` — split before lines starting with `#` |
| Lookbehind | `(?<=...)` | `(?<=\n)` — split after newlines |
| Non-capturing group | `(?:...)` | `(?:---|___|\*\*\*)` — match any horizontal rule |
| Multiline mode | `(?m)` | `(?m)^#{1,3}\s` — `^` matches start of each line |
| Character classes | `\d`, `\w`, `\s` | `\d{4}-\d{2}-\d{2}` — date pattern |
| Alternation | `\|` | `\n\n\|\r\n\r\n` — double newline (Unix or Windows) |
| Quantifiers | `+`, `*`, `{n,m}` | `\n{2,}` — two or more newlines |

> **Tip:** When sending patterns in JSON, backslashes must be double-escaped: `\\n` for a newline, `\\d` for a digit, etc.

---

## Patterns by Use Case

### Markdown

| Use Case | Pattern | Description |
|----------|---------|-------------|
| Any heading (h1–h3) | `(?=^#{1,3}\s)` | Splits before lines starting with 1–3 `#` characters |
| Any heading (h1–h6) | `(?=^#{1,6}\s)` | Splits before lines starting with 1–6 `#` characters |
| Only h1 headings | `(?=^#\s)` | Splits before top-level headings only |
| H1 and h2 headings | `(?=^#{1,2}\s)` | Splits at major section boundaries |
| Horizontal rules | `(?=^[-_*]{3,}\s*$)` | Splits before `---`, `___`, or `***` lines |
| Fenced code blocks | ` (?=^```)`  | Splits before fenced code block delimiters |

#### Example: Split on h1–h3 headings

**Pattern:** `(?=^#{1,3}\s)`

**Input:**
```markdown
# Getting Started
Install the SDK with pip install partio.

## Configuration
Set your API key in the environment.

### Advanced Options
You can customize token limits and overlap.

# API Reference
Full endpoint documentation follows.
```

**Output chunks:**
```
Chunk 1:
  # Getting Started
  Install the SDK with pip install partio.

Chunk 2:
  ## Configuration
  Set your API key in the environment.

Chunk 3:
  ### Advanced Options
  You can customize token limits and overlap.

Chunk 4:
  # API Reference
  Full endpoint documentation follows.
```

#### Example: Split on h1 headings only

**Pattern:** `(?=^#\s)`

**Same input as above — Output chunks:**
```
Chunk 1:
  # Getting Started
  Install the SDK with pip install partio.

  ## Configuration
  Set your API key in the environment.

  ### Advanced Options
  You can customize token limits and overlap.

Chunk 2:
  # API Reference
  Full endpoint documentation follows.
```

> Note: `##` and `###` headings are *not* split points here, so they stay grouped under the preceding `#` heading.

#### Example: Split on horizontal rules

**Pattern:** `(?=^[-_*]{3,}\s*$)`

**Input:**
```
First section content here.

---

Second section content here.

***

Third section content here.
```

**Output chunks:**
```
Chunk 1:
  First section content here.

Chunk 2:
  ---

  Second section content here.

Chunk 3:
  ***

  Third section content here.
```

**Request (JSON):**
```json
{
    "Type": "Text",
    "Text": "# Introduction\nPartio is a chunking platform.\n\n# Architecture\nPartio uses a modular design.\n\n# Deployment\nUse Docker Compose to deploy.",
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=^#{1,3}\\s)",
        "FixedTokenCount": 512
    }
}
```

---

### Plain Text and Prose

| Use Case | Pattern | Description |
|----------|---------|-------------|
| Double newline (paragraphs) | `\n\n+` | Splits on one or more blank lines |
| Double newline (cross-platform) | `(?:\r?\n){2,}` | Handles both `\n\n` and `\r\n\r\n` |
| Page breaks | `\f` | Splits on form feed characters (common in PDFs converted to text) |
| Sentence boundaries (simple) | `(?<=\.)\s+(?=[A-Z])` | Splits after a period followed by whitespace and an uppercase letter |
| Line breaks | `\r?\n` | Splits on every line (use with a large `FixedTokenCount` to regroup) |

#### Example: Split on double newlines (paragraphs)

**Pattern:** `\n\n+`

**Input:**
```
The quick brown fox jumps over the lazy dog. This is the first paragraph
with multiple sentences that flow together.

The second paragraph discusses something entirely different. It has its
own context and meaning.


The third paragraph is separated by two blank lines. The extra blank
line makes no difference — the pattern matches one or more.

The fourth paragraph wraps things up.
```

**Output chunks:**
```
Chunk 1:
  The quick brown fox jumps over the lazy dog. This is the first paragraph
  with multiple sentences that flow together.

Chunk 2:
  The second paragraph discusses something entirely different. It has its
  own context and meaning.

Chunk 3:
  The third paragraph is separated by two blank lines. The extra blank
  line makes no difference — the pattern matches one or more.

Chunk 4:
  The fourth paragraph wraps things up.
```

> Note: The blank lines themselves are consumed by the split (they are the delimiter). Unlike lookahead patterns, `\n\n+` removes the matched text from the output.

#### Example: Split on sentence boundaries

**Pattern:** `(?<=\.)\s+(?=[A-Z])`

**Input:**
```
Partio is a chunking platform. It supports multiple strategies. The RegexBased
strategy is the most flexible. Users can define custom split points.
```

**Output chunks:**
```
Chunk 1:
  Partio is a chunking platform.

Chunk 2:
  It supports multiple strategies.

Chunk 3:
  The RegexBased strategy is the most flexible.

Chunk 4:
  Users can define custom split points.
```

> Note: With a `FixedTokenCount` of 256, these small sentences would be grouped back together into fewer chunks. Lower the token budget if you want fine-grained sentence-level chunks.

**Request (JSON):**
```json
{
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "\\n\\n+",
        "FixedTokenCount": 256
    }
}
```

---

### Log Files

| Use Case | Pattern | Description |
|----------|---------|-------------|
| ISO timestamps | `(?=\d{4}-\d{2}-\d{2}[T ])` | Splits before `2024-01-15T` or `2024-01-15 ` |
| ISO timestamps (line start) | `(?=^\d{4}-\d{2}-\d{2}[T ])` | Same, but only at line beginnings |
| Syslog-style | `(?=^[A-Z][a-z]{2}\s+\d{1,2}\s)` | Splits before `Jan 15 `, `Feb  3 `, etc. |
| Bracketed timestamps | `(?=^\[[\d/: ]+\])` | Splits before `[2024/01/15 10:30:00]` |
| Log level markers | `(?=^\[?(DEBUG\|INFO\|WARN\|ERROR\|FATAL)\]?)` | Splits before log level keywords |

#### Example: Split on ISO timestamps

**Pattern:** `(?=^\d{4}-\d{2}-\d{2}[T ])`

**Input:**
```
2024-01-15T10:30:00Z INFO  Application started successfully
  Loading configuration from /etc/app/config.yaml
  Connected to database on port 5432
2024-01-15T10:30:05Z WARN  Cache miss for key "user_session_abc123"
  Falling back to database lookup
2024-01-15T10:30:06Z ERROR Connection refused to redis://cache:6379
  Retrying in 5 seconds...
  Retry attempt 1 of 3
2024-01-15T10:30:11Z INFO  Redis connection re-established
```

**Output chunks:**
```
Chunk 1:
  2024-01-15T10:30:00Z INFO  Application started successfully
    Loading configuration from /etc/app/config.yaml
    Connected to database on port 5432

Chunk 2:
  2024-01-15T10:30:05Z WARN  Cache miss for key "user_session_abc123"
    Falling back to database lookup

Chunk 3:
  2024-01-15T10:30:06Z ERROR Connection refused to redis://cache:6379
    Retrying in 5 seconds...
    Retry attempt 1 of 3

Chunk 4:
  2024-01-15T10:30:11Z INFO  Redis connection re-established
```

> Note: Multi-line log entries (with indented continuation lines) stay grouped with their timestamp because only lines starting with a timestamp trigger a split.

#### Example: Split on syslog-style timestamps

**Pattern:** `(?=^[A-Z][a-z]{2}\s+\d{1,2}\s)`

**Input:**
```
Jan 15 10:30:00 webserver sshd[1234]: Accepted publickey for admin
Jan 15 10:30:05 webserver nginx[5678]: GET /api/health 200 0.003s
Jan 15 10:31:12 webserver sshd[1234]: Disconnected from user admin
```

**Output chunks:**
```
Chunk 1:
  Jan 15 10:30:00 webserver sshd[1234]: Accepted publickey for admin

Chunk 2:
  Jan 15 10:30:05 webserver nginx[5678]: GET /api/health 200 0.003s

Chunk 3:
  Jan 15 10:31:12 webserver sshd[1234]: Disconnected from user admin
```

**Request (JSON):**
```json
{
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=^\\d{4}-\\d{2}-\\d{2}[T ])",
        "FixedTokenCount": 512
    }
}
```

---

### Source Code

| Use Case | Pattern | Description |
|----------|---------|-------------|
| Python functions | `(?=^def\s)` | Splits before `def function_name(` |
| Python classes | `(?=^class\s)` | Splits before `class ClassName` |
| Python functions and classes | `(?=^(?:def\|class)\s)` | Splits before either |
| JavaScript/TypeScript functions | `(?=^(?:function\|export\s+function\|const\s+\w+\s*=\s*(?:\(?\w*\)?\s*=>))\s*)` | Splits before function declarations and arrow functions |
| C# / Java methods | `(?=^\s*(?:public\|private\|protected\|internal\|static)[\s\w<>\[\]]*\s+\w+\s*\()` | Splits before method signatures |
| Go functions | `(?=^func\s)` | Splits before `func FunctionName(` |
| Rust functions | `(?=^(?:pub\s+)?fn\s)` | Splits before `fn` or `pub fn` |
| Generic blank-line separator | `\n\s*\n` | Splits on blank lines (works for most languages between top-level declarations) |

#### Example: Split Python functions and classes

**Pattern:** `(?=^(?:def|class)\s)`

**Input:**
```python
import os
import sys

class FileProcessor:
    def __init__(self, path):
        self.path = path

    def process(self):
        with open(self.path) as f:
            return f.read()

def validate_path(path):
    if not os.path.exists(path):
        raise FileNotFoundError(path)
    return True

def main():
    processor = FileProcessor("/tmp/data.txt")
    if validate_path(processor.path):
        result = processor.process()
        print(result)
```

**Output chunks:**
```
Chunk 1:
  import os
  import sys

Chunk 2:
  class FileProcessor:
      def __init__(self, path):
          self.path = path

      def process(self):
          with open(self.path) as f:
              return f.read()

Chunk 3:
  def validate_path(path):
      if not os.path.exists(path):
          raise FileNotFoundError(path)
      return True

Chunk 4:
  def main():
      processor = FileProcessor("/tmp/data.txt")
      if validate_path(processor.path):
          result = processor.process()
          print(result)
```

> Note: The `def __init__` and `def process` methods inside the class do *not* trigger splits because they are indented — the `^` anchor only matches at the start of a line, and these methods start with whitespace. Only top-level `def` and `class` declarations split.

#### Example: Split C# methods

**Pattern:** `(?=^\s*(?:public|private|protected|internal|static)[\s\w<>\[\]]*\s+\w+\s*\()`

**Input:**
```csharp
using System;

namespace MyApp
{
    public class Calculator
    {
        private int _value;

        public Calculator(int initial)
        {
            _value = initial;
        }

        public int Add(int x)
        {
            _value += x;
            return _value;
        }

        public int Subtract(int x)
        {
            _value -= x;
            return _value;
        }

        private void Reset()
        {
            _value = 0;
        }
    }
}
```

**Output chunks:**
```
Chunk 1:
  using System;

  namespace MyApp
  {

Chunk 2:
      public class Calculator
      {
          private int _value;

Chunk 3:
          public Calculator(int initial)
          {
              _value = initial;
          }

Chunk 4:
          public int Add(int x)
          {
              _value += x;
              return _value;
          }

Chunk 5:
          public int Subtract(int x)
          {
              _value -= x;
              return _value;
          }

Chunk 6:
          private void Reset()
          {
              _value = 0;
          }
      }
  }
```

#### Example: Split Go functions

**Pattern:** `(?=^func\s)`

**Input:**
```go
package main

import "fmt"

func add(a, b int) int {
    return a + b
}

func multiply(a, b int) int {
    return a * b
}

func main() {
    fmt.Println(add(2, 3))
    fmt.Println(multiply(4, 5))
}
```

**Output chunks:**
```
Chunk 1:
  package main

  import "fmt"

Chunk 2:
  func add(a, b int) int {
      return a + b
  }

Chunk 3:
  func multiply(a, b int) int {
      return a * b
  }

Chunk 4:
  func main() {
      fmt.Println(add(2, 3))
      fmt.Println(multiply(4, 5))
  }
```

**Request (JSON):**
```json
{
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=^(?:def|class)\\s)",
        "FixedTokenCount": 512
    }
}
```

---

### Structured Data Formats

| Use Case | Pattern | Description |
|----------|---------|-------------|
| XML/HTML closing tags | `(?<=<\/\w+>)\s*` | Splits after closing tags like `</div>`, `</section>` |
| XML/HTML section tags | `(?=<(?:section\|article\|chapter\|div)[\s>])` | Splits before opening section-level tags |
| LaTeX sections | `(?=\\\\(?:sub)*section\{)` | Splits before `\section{`, `\subsection{`, `\subsubsection{` |
| LaTeX chapters and sections | `(?=\\\\(?:chapter\|(?:sub)*section)\{)` | Also includes `\chapter{` |
| YAML document separators | `(?=^---\s*$)` | Splits before YAML document boundaries |
| INI file sections | `(?=^\[.+\]\s*$)` | Splits before `[SectionName]` headers |
| Org-mode headings | `(?=^\*+\s)` | Splits before Org-mode `*`, `**`, `***` headings |
| RST headings (underline) | `(?=^[^\n]+\n[=\-~^]+\s*$)` | Splits before reStructuredText heading + underline pairs |

#### Example: Split HTML on section tags

**Pattern:** `(?=<(?:section|article|div)[\s>])`

**Input:**
```html
<article>
  <h1>Welcome</h1>
  <p>This is the introduction.</p>
</article>
<section id="features">
  <h2>Features</h2>
  <p>Partio supports many strategies.</p>
</section>
<section id="pricing">
  <h2>Pricing</h2>
  <p>Free for open-source projects.</p>
</section>
```

**Output chunks:**
```
Chunk 1:
  <article>
    <h1>Welcome</h1>
    <p>This is the introduction.</p>
  </article>

Chunk 2:
  <section id="features">
    <h2>Features</h2>
    <p>Partio supports many strategies.</p>
  </section>

Chunk 3:
  <section id="pricing">
    <h2>Pricing</h2>
    <p>Free for open-source projects.</p>
  </section>
```

#### Example: Split LaTeX on sections

**Pattern:** `(?=\\(?:sub)*section\{)`

**Input:**
```latex
\section{Introduction}
Partio is a chunking platform designed for RAG pipelines.

\subsection{Motivation}
Existing tools lack flexible delimiter support.

\section{Architecture}
The system uses a modular pipeline approach.

\subsection{Chunking Engine}
The engine dispatches to strategy-specific chunkers.
```

**Output chunks:**
```
Chunk 1:
  \section{Introduction}
  Partio is a chunking platform designed for RAG pipelines.

Chunk 2:
  \subsection{Motivation}
  Existing tools lack flexible delimiter support.

Chunk 3:
  \section{Architecture}
  The system uses a modular pipeline approach.

Chunk 4:
  \subsection{Chunking Engine}
  The engine dispatches to strategy-specific chunkers.
```

#### Example: Split INI file on sections

**Pattern:** `(?=^\[.+\]\s*$)`

**Input:**
```ini
; Global settings
debug = false
log_level = info

[database]
host = localhost
port = 5432
name = partio_db

[cache]
enabled = true
ttl = 3600
backend = redis

[api]
port = 8400
rate_limit = 100
```

**Output chunks:**
```
Chunk 1:
  ; Global settings
  debug = false
  log_level = info

Chunk 2:
  [database]
  host = localhost
  port = 5432
  name = partio_db

Chunk 3:
  [cache]
  enabled = true
  ttl = 3600
  backend = redis

Chunk 4:
  [api]
  port = 8400
  rate_limit = 100
```

#### Example: Split YAML on document separators

**Pattern:** `(?=^---\s*$)`

**Input:**
```yaml
---
name: service-a
replicas: 3
image: app:latest
---
name: service-b
replicas: 1
image: worker:latest
---
name: service-c
replicas: 2
image: api:latest
```

**Output chunks:**
```
Chunk 1:
  ---
  name: service-a
  replicas: 3
  image: app:latest

Chunk 2:
  ---
  name: service-b
  replicas: 1
  image: worker:latest

Chunk 3:
  ---
  name: service-c
  replicas: 2
  image: api:latest
```

**Request (JSON):**
```json
{
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=\\\\(?:sub)*section\\{)",
        "FixedTokenCount": 1024
    }
}
```

---

### Legal and Business Documents

| Use Case | Pattern | Description |
|----------|---------|-------------|
| Numbered sections | `(?=^\d+\.\s)` | Splits before `1. `, `2. `, `15. ` |
| Nested numbered sections | `(?=^\d+(?:\.\d+)*\.?\s)` | Splits before `1.`, `1.1`, `2.3.1`, etc. |
| Article / Section labels | `(?=^(?:Article\|Section\|Clause)\s+\d)` | Splits before `Article 1`, `Section 3`, `Clause 7` |
| Uppercase headings | `(?=^[A-Z][A-Z\s]{4,}$)` | Splits before all-caps headings (5+ chars) |
| Roman numeral sections | `(?=^(?:I\|II\|III\|IV\|V\|VI\|VII\|VIII\|IX\|X)+\.\s)` | Splits before `I. `, `IV. `, etc. |
| Lettered subsections | `(?=^\(?[a-z]\)\s)` | Splits before `(a) `, `(b) `, etc. |

#### Example: Split on Article / Section labels

**Pattern:** `(?=^(?:Article|Section)\s+\d)`

**Input:**
```
SERVICES AGREEMENT

Article 1 — Definitions
"Client" means the entity entering into this agreement.
"Provider" means Partio, Inc.

Article 2 — Scope of Services
Provider shall deliver the chunking platform as described
in Exhibit A attached hereto.

Section 2.1 — Deployment
Provider shall deploy within 30 calendar days of execution.

Section 2.2 — Support
Provider shall offer 24/7 support via email and phone.

Article 3 — Payment Terms
Client shall pay all invoices within 30 days of receipt.
```

**Output chunks:**
```
Chunk 1:
  SERVICES AGREEMENT

Chunk 2:
  Article 1 — Definitions
  "Client" means the entity entering into this agreement.
  "Provider" means Partio, Inc.

Chunk 3:
  Article 2 — Scope of Services
  Provider shall deliver the chunking platform as described
  in Exhibit A attached hereto.

Chunk 4:
  Section 2.1 — Deployment
  Provider shall deploy within 30 calendar days of execution.

Chunk 5:
  Section 2.2 — Support
  Provider shall offer 24/7 support via email and phone.

Chunk 6:
  Article 3 — Payment Terms
  Client shall pay all invoices within 30 days of receipt.
```

#### Example: Split on nested numbered sections

**Pattern:** `(?=^\d+(?:\.\d+)*\.?\s)`

**Input:**
```
1. Introduction
This document describes the project requirements.

1.1 Purpose
Define the system architecture.

1.2 Scope
Cover backend and frontend components.

2. Requirements
The following requirements apply.

2.1 Functional Requirements
The system shall support chunking.

2.1.1 Text Chunking
Plain text must be supported.

2.1.2 Table Chunking
HTML tables must be supported.
```

**Output chunks:**
```
Chunk 1:
  1. Introduction
  This document describes the project requirements.

Chunk 2:
  1.1 Purpose
  Define the system architecture.

Chunk 3:
  1.2 Scope
  Cover backend and frontend components.

Chunk 4:
  2. Requirements
  The following requirements apply.

Chunk 5:
  2.1 Functional Requirements
  The system shall support chunking.

Chunk 6:
  2.1.1 Text Chunking
  Plain text must be supported.

Chunk 7:
  2.1.2 Table Chunking
  HTML tables must be supported.
```

**Request (JSON):**
```json
{
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=^(?:Article|Section)\\s+\\d)",
        "FixedTokenCount": 1024
    }
}
```

---

### Email and Conversations

| Use Case | Pattern | Description |
|----------|---------|-------------|
| Email headers (forwarded/reply) | `(?=^(?:From\|To\|Date\|Subject):\s)` | Splits before email header lines |
| Email reply chains | `(?=^>+\s)` | Splits before quoted reply lines |
| Chat message timestamps | `(?=^\[\d{1,2}:\d{2}(?::\d{2})?\])` | Splits before `[10:30]` or `[10:30:45]` |
| Chat usernames | `(?=^[\w]+:\s)` | Splits before `Username: message` |
| Separator lines | `-{3,}\|={3,}\|_{3,}` | Splits on lines of dashes, equals, or underscores |

#### Example: Split chat transcript on timestamps

**Pattern:** `(?=^\[\d{1,2}:\d{2}\])`

**Input:**
```
[10:30] alice: Hey, has anyone tested the new regex chunking?
[10:31] bob: Yes, I tried it with markdown headings. Works great.
[10:31] bob: The lookahead patterns keep the heading text in the chunk.
[10:33] carol: I'm testing with log files right now. Splitting on ISO timestamps.
[10:35] alice: Perfect. Let me know if you find edge cases.
```

**Output chunks:**
```
Chunk 1:
  [10:30] alice: Hey, has anyone tested the new regex chunking?

Chunk 2:
  [10:31] bob: Yes, I tried it with markdown headings. Works great.

Chunk 3:
  [10:31] bob: The lookahead patterns keep the heading text in the chunk.

Chunk 4:
  [10:33] carol: I'm testing with log files right now. Splitting on ISO timestamps.

Chunk 5:
  [10:35] alice: Perfect. Let me know if you find edge cases.
```

> Note: With a higher `FixedTokenCount`, consecutive messages would be grouped together into fewer chunks.

#### Example: Split email thread on headers

**Pattern:** `(?=^(?:From|Date|Subject):\s)`

**Input:**
```
From: alice@example.com
Date: 2024-01-15
Subject: Regex chunking feedback

Hi Bob, the regex chunking feature looks great.

From: bob@example.com
Date: 2024-01-16
Subject: Re: Regex chunking feedback

Thanks Alice! I've added more patterns to the docs.
```

**Output chunks:**
```
Chunk 1:
  From: alice@example.com

Chunk 2:
  Date: 2024-01-15

Chunk 3:
  Subject: Regex chunking feedback

  Hi Bob, the regex chunking feature looks great.

Chunk 4:
  From: bob@example.com

Chunk 5:
  Date: 2024-01-16

Chunk 6:
  Subject: Re: Regex chunking feedback

  Thanks Alice! I've added more patterns to the docs.
```

> Note: This splits on *every* header line. If you want to split only between entire email messages, use a pattern that targets the `From:` line only: `(?=^From:\s)`

---

### Scientific and Academic

| Use Case | Pattern | Description |
|----------|---------|-------------|
| Numbered references | `(?=^\[\d+\]\s)` | Splits before `[1] `, `[23] `, etc. |
| DOI references | `(?=\b10\.\d{4,}/)` | Splits before DOI identifiers |
| Figure/Table captions | `(?=^(?:Figure\|Table\|Fig\.)\s+\d)` | Splits before `Figure 1`, `Table 3`, etc. |
| Abstract/Introduction/etc. | `(?=^(?:Abstract\|Introduction\|Methods\|Results\|Discussion\|Conclusion\|References)\s*$)` | Splits before standard paper section headings |

#### Example: Split paper on standard section headings

**Pattern:** `(?=^(?:Abstract|Introduction|Methods|Results|Discussion|Conclusion|References)\s*$)`

**Input:**
```
Abstract
We present a novel approach to text chunking using regular
expressions as user-defined split delimiters.

Introduction
Text chunking is a critical preprocessing step in RAG pipelines.
Most existing tools offer only fixed-size or sentence-based splitting.

Methods
We implement regex-based splitting using .NET's Regex.Split with
a configurable token budget for segment grouping.

Results
Our approach correctly splits 15 document formats tested, including
Markdown, LaTeX, log files, and legal contracts.

Conclusion
Regex-based chunking provides a flexible, format-agnostic solution.
```

**Output chunks:**
```
Chunk 1:
  Abstract
  We present a novel approach to text chunking using regular
  expressions as user-defined split delimiters.

Chunk 2:
  Introduction
  Text chunking is a critical preprocessing step in RAG pipelines.
  Most existing tools offer only fixed-size or sentence-based splitting.

Chunk 3:
  Methods
  We implement regex-based splitting using .NET's Regex.Split with
  a configurable token budget for segment grouping.

Chunk 4:
  Results
  Our approach correctly splits 15 document formats tested, including
  Markdown, LaTeX, log files, and legal contracts.

Chunk 5:
  Conclusion
  Regex-based chunking provides a flexible, format-agnostic solution.
```

#### Example: Split numbered references

**Pattern:** `(?=^\[\d+\]\s)`

**Input:**
```
[1] Smith, J. (2023). "Text Chunking for RAG." Journal of AI, 45(2), 112-128.
[2] Zhang, W. & Lee, K. (2024). "Embedding Quality and Chunk Size." NeurIPS 2024.
[3] Patel, R. (2023). "Regex-Based Document Segmentation." ACL Proceedings, 891-903.
```

**Output chunks:**
```
Chunk 1:
  [1] Smith, J. (2023). "Text Chunking for RAG." Journal of AI, 45(2), 112-128.

Chunk 2:
  [2] Zhang, W. & Lee, K. (2024). "Embedding Quality and Chunk Size." NeurIPS 2024.

Chunk 3:
  [3] Patel, R. (2023). "Regex-Based Document Segmentation." ACL Proceedings, 891-903.
```

---

## Combining Patterns

You can combine multiple patterns with alternation (`|`) to split on several boundary types at once.

**Example — Markdown headings OR horizontal rules:**
```
(?=^#{1,3}\s)|(?=^[-_*]{3,}\s*$)
```

**Example — Python functions OR classes OR blank lines:**
```
(?=^(?:def|class)\s)|\n\s*\n
```

#### Example: Combined Markdown headings and horizontal rules

**Pattern:** `(?=^#{1,3}\s)|(?=^[-_*]{3,}\s*$)`

**Input:**
```markdown
# Overview
This is the overview section.

---

## Details
Here are the details.

## Summary
Final thoughts here.

***

# Appendix
Additional notes.
```

**Output chunks:**
```
Chunk 1:
  # Overview
  This is the overview section.

Chunk 2:
  ---

Chunk 3:
  ## Details
  Here are the details.

Chunk 4:
  ## Summary
  Final thoughts here.

Chunk 5:
  ***

Chunk 6:
  # Appendix
  Additional notes.
```

**JSON (remember to double-escape):**
```json
{
    "RegexPattern": "(?=^#{1,3}\\s)|(?=^[-_*]{3,}\\s*$)"
}
```

---

## Tips and Best Practices

1. **Use lookaheads to keep boundary text.** A pattern like `\n\n+` *removes* the blank lines from the output. A pattern like `(?=^# )` keeps the `# ` in the resulting segment because lookaheads are zero-width.

2. **Anchor with `^` when possible.** Anchoring to line start prevents mid-line false matches. In .NET, `^` matches at the start of each line by default when using `RegexOptions.Multiline` — Partio enables this via the pattern inline flag `(?m)` if needed.

3. **Set an appropriate `FixedTokenCount`.** After splitting, segments are grouped up to this token limit. If your sections are large, increase the budget (e.g., 512 or 1024). If they're small, a lower budget keeps chunks focused.

4. **Test your pattern first.** Before sending to Partio, verify your pattern produces the expected splits using a tool like [regex101.com](https://regex101.com/) (select the `.NET` flavor).

5. **Avoid catastrophic backtracking.** Partio enforces a 5-second regex timeout, but patterns with nested quantifiers (e.g., `(a+)+`) can still cause slow responses. Keep patterns simple and specific.

6. **Use overlap for context continuity.** Set `OverlapCount` to 1 or 2 so each chunk includes trailing segments from the previous chunk — useful for semantic search where boundary context matters.

7. **Watch for empty segments.** Partio automatically discards empty/whitespace segments after splitting, so you don't need to worry about consecutive delimiters producing blanks.

8. **JSON escaping matters.** In JSON strings, backslashes must be doubled:
   - Regex `\d` becomes JSON `"\\d"`
   - Regex `\n` becomes JSON `"\\n"`
   - Regex `\\section` becomes JSON `"\\\\section"`

---

## Quick Reference Card

| Document Type | Recommended Pattern | Token Budget |
|---------------|-------------------|--------------|
| Markdown docs | `(?=^#{1,3}\s)` | 512 |
| Plain text / prose | `\n\n+` | 256 |
| Server logs | `(?=^\d{4}-\d{2}-\d{2}[T ])` | 512 |
| Python source | `(?=^(?:def\|class)\s)` | 512 |
| C# / Java source | `(?=^\s*(?:public\|private\|protected)[\s\w]*\s+\w+\s*\()` | 512 |
| LaTeX documents | `(?=\\\\(?:sub)*section\{)` | 1024 |
| Legal contracts | `(?=^(?:Article\|Section)\s+\d)` | 1024 |
| XML / HTML | `(?=<(?:section\|article\|div)[\s>])` | 512 |
| INI / config files | `(?=^\[.+\]\s*$)` | 256 |
| Email threads | `(?=^(?:From\|Date\|Subject):\s)` | 512 |
