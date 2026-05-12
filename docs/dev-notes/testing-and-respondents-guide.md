# Chapter 5 — Testing Methodology & Respondents Guide

For the research paper on **Axiom of the Broken Sun** (voice-activated spell casting + chemistry concepts).

## Recommended Testing (multi-instrument)

Because the game has **three distinct value claims**, three corresponding evaluations are needed. Do not collapse them into one survey.

| Claim being tested | Instrument | Why this one |
|---|---|---|
| **Software quality** ("the game works well") | **ISO/IEC 25010** evaluation questionnaire (Functional Suitability, Performance Efficiency, Usability, Reliability, Compatibility, Maintainability) | Standard for PH IT capstones; gives a defensible quality score per characteristic |
| **Usability of the voice-spell mechanic** | **System Usability Scale (SUS)** — 10-item, 5-point Likert, scores 0–100 | Industry standard, easy to defend, benchmark of 68 = average |
| **Learning effectiveness of chemistry concepts** | **Pre-test / Post-test** on the chemistry concepts taught (e.g., states of matter, reactions, elements encoded in spells), analyzed with **paired-samples t-test** or **Wilcoxon signed-rank** | The only way to claim the game actually teaches; without it Chapter 5 has no learning finding |

Optional additions if the panel asks for more rigor:

- **GUESS-18** (Game User Experience Satisfaction Scale) — better than ad-hoc "fun" questions
- **TAM** (Perceived Usefulness + Perceived Ease of Use) — useful if framing it as an educational technology adoption study

## Respondents — Sample Size

Split by respondent type, because each instrument needs a different population.

### 1. End-users / players (students)
For SUS, ISO 25010 usability/functional, and pre/post chemistry test.

- **Slovin's formula:** n = N / (1 + Ne²), e = 0.05
  - Target population of one section/class of ~40 students → ~36
  - One year-level of ~200 → ~133
- **Practical floor: 30 respondents.** Below 30, t-tests lose power and reviewers will flag it.
- **Sweet spot for a capstone: 30–50.**

### 2. IT / Software experts
For ISO 25010 Maintainability, Performance, Reliability, Security.

- **3–5 experts** is standard and accepted (Nielsen's heuristic threshold).
- Faculty, professional developers, or thesis adviser-approved evaluators.

### 3. Chemistry / subject-matter experts
To validate that the in-game chemistry is accurate and pedagogically sound.

- **3 SMEs** (chemistry teachers / professors).
- This is what the panel will ask about; without it the "chemistry concepts" claim is unsupported.

## What Chapter 5 Should Report

- ISO 25010 mean per characteristic + verbal interpretation (e.g., 4.21 = "Very Acceptable")
- SUS score with industry-benchmark comparison
- Pre-test vs post-test mean, gain score, and t-test p-value (headline learning finding)
- Voice recognition accuracy log (intended spell vs recognized spell rate) — already collectable from the game, include it as a technical finding alongside the perception data
