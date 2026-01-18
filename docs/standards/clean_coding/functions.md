# Functions

## Agent Summary

**Quick Rules for Functions:**
- Small (< 20 lines ideally)
- Do ONE thing only
- One level of abstraction
- 0-3 arguments maximum
- No side effects
- Descriptive names (long is OK)
- No flag arguments
- Extract try/catch blocks

**Key Actions:**
1. Split large functions into smaller ones
2. Replace flag arguments with separate functions
3. Convert argument lists to objects when >2 args
4. Extract complex conditionals into named functions
5. Ensure functions either command OR query, not both

---

Functions are the first line of organization in any program. They should be small, do one thing, and do it well.

## Small!

**The first rule of functions is that they should be small. The second rule is that they should be smaller than that.**

### Blocks and Indenting

- Blocks within `if`, `else`, `while` statements should be one line long
- That line should probably be a function call
- Functions should not be large enough to hold nested structures
- Indent level should not be greater than one or two

## Do One Thing

**FUNCTIONS SHOULD DO ONE THING. THEY SHOULD DO IT WELL. THEY SHOULD DO IT ONLY.**

### How to Know if a Function Does One Thing

- Can you extract another function from it with a name that is not merely a restatement?
- Does it have sections like declarations, initialization, etc.? If yes, it's doing more than one thing

## One Level of Abstraction per Function

All statements within a function should be at the same level of abstraction.

### The Stepdown Rule

We want code to read like a top-down narrative:
```
To include the setups and teardowns:
  - we include setups
  - then we include the test page content
  - then we include the teardowns

To include the setups:
  - we include the suite setup if this is a suite
  - then we include the regular setup
```

## Switch Statements

- Hard to make small
- Always do N things by nature
- Bury them in low-level classes
- Never repeat them
- Use polymorphism to avoid them

## Use Descriptive Names

- A long descriptive name is better than a short enigmatic name
- A long descriptive name is better than a long descriptive comment
- Don't be afraid to make names long
- Choosing good names often leads to favorable restructuring

## Function Arguments

### Ideal Number of Arguments

1. **Zero (niladic)** - Best
2. **One (monadic)** - Good
3. **Two (dyadic)** - Acceptable
4. **Three (triadic)** - Avoid where possible
5. **More than three (polyadic)** - Requires special justification

### Common Monadic Forms

Two common reasons for single arguments:
1. Asking a question: `boolean fileExists("MyFile")`
2. Transforming and returning: `InputStream fileOpen("MyFile")`

### Flag Arguments

**Flag arguments are terrible!** They proclaim the function does more than one thing.

Instead of:
```java
render(boolean isSuite)
```

Write:
```java
renderForSuite()
renderForSingleTest()
```

### Dyadic Functions

- Harder to understand than monadic
- Natural dyads are acceptable: `Point(x, y)`
- Convert when possible: `writeField(outputStream, name)` → `outputStream.writeField(name)`

### Triads

- Significantly harder to understand
- Think very carefully before creating

### Argument Objects

When functions need multiple arguments, consider grouping them:

```java
// Bad
Circle makeCircle(double x, double y, double radius);

// Good
Circle makeCircle(Point center, double radius);
```

### Verbs and Keywords

- Function and argument should form verb/noun pair: `write(name)`
- Even better: `writeField(name)`
- Keyword form: `assertExpectedEqualsActual(expected, actual)`

## Have No Side Effects

Functions should either do something or answer something, but not both.

### Command Query Separation

- Functions should either change state OR return information
- Never both

Bad:
```java
public boolean set(String attribute, String value);
```

Good:
```java
public void setAttribute(String attribute, String value);
public boolean attributeExists(String attribute);
```

## Prefer Exceptions to Returning Error Codes

Returning error codes violates command query separation and leads to nested structures.

Bad:
```java
if (deletePage(page) == E_OK) {
  if (registry.deleteReference(page.name) == E_OK) {
    // ...
  }
}
```

Good:
```java
try {
  deletePage(page);
  registry.deleteReference(page.name);
  // ...
} catch (Exception e) {
  logger.log(e.getMessage());
}
```

### Extract Try/Catch Blocks

Try/catch blocks are ugly. Extract their bodies into functions:

```java
public void delete(Page page) {
  try {
    deletePageAndAllReferences(page);
  } catch (Exception e) {
    logError(e);
  }
}

private void deletePageAndAllReferences(Page page) throws Exception {
  deletePage(page);
  registry.deleteReference(page.name);
  configKeys.deleteKey(page.name.makeKey());
}
```

## Don't Repeat Yourself (DRY)

- Duplication is the root of all evil in software
- Many principles exist to control/eliminate it
- Every piece of knowledge should have a single, unambiguous representation

## Structured Programming

Dijkstra's rules (less important for small functions):
- One entry and one exit per function
- Only one `return` statement
- No `break` or `continue` in loops
- Never use `goto`

For small functions, multiple returns, break, and continue can be more expressive.

## Writing Functions

- First draft is usually long and complicated
- Refine with successive refinement
- Split out functions
- Change names
- Eliminate duplication
- Shrink and reorder
- All while keeping tests passing

## Summary

Master programmers think of systems as stories to be told. Functions are the verbs of that language, classes are the nouns. The art of programming is the art of language design.

**Key Points:**
- Small functions
- Do one thing
- One level of abstraction
- Few arguments
- No side effects
- Descriptive names