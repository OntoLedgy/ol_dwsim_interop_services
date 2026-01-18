# Smells and Heuristics

## Agent Summary

**Top Code Smells to Fix:**
- Duplication (DRY violation)
- Long functions/classes
- Dead code
- Inconsistent conventions
- Too many arguments
- Feature envy
- Comments explaining bad code

**Key Actions:**
1. Delete dead code immediately
2. Extract methods when functions > 20 lines
3. Replace magic numbers with constants
4. Make dependencies explicit
5. Prefer polymorphism over switch statements
6. Keep tests fast
7. Follow team conventions consistently

---

A comprehensive reference of code smells and heuristics for writing clean code.

## Comments

### C1: Inappropriate Information
- Reserve comments for technical notes about code and design
- Don't include metadata that belongs in version control

### C2: Obsolete Comment
- Update or delete obsolete comments immediately
- Old comments become lies

### C3: Redundant Comment
- Don't describe something that adequately describes itself
- Comments should add value, not repeat code

### C4: Poorly Written Comment
- Comments should be brief, concise, correctly spelled
- Take time to write them well

### C5: Commented-Out Code
- Delete commented-out code immediately
- Version control remembers everything

## Environment

### E1: Build Requires More Than One Step
- Building should be a single trivial operation
- One command checkout, one command build

### E2: Tests Require More Than One Step
- Running tests should be trivial
- One button click or one command

## Functions

### F1: Too Many Arguments
- Zero arguments is best
- One is good, two is okay, three should be avoided
- More than three requires special justification

### F2: Output Arguments
- Arguments are for inputs
- If state must change, change the state of the owning object

### F3: Flag Arguments
- Boolean arguments loudly declare function does more than one thing
- Split into separate functions

### F4: Dead Function
- Delete methods that are never called
- Version control will remember them

## General

### G1: Multiple Languages in One Source File
- Minimize the number of languages per file
- Ideally, only one language per source file

### G2: Obvious Behavior Is Unimplemented
- Functions should implement behaviors that programmers expect
- Principle of Least Surprise

### G3: Incorrect Behavior at the Boundaries
- Look for every boundary condition
- Write tests for every boundary

### G4: Overridden Safeties
- Don't turn off compiler warnings
- Don't override safety mechanisms

### G5: Duplication
- Follow DRY (Don't Repeat Yourself)
- Duplication is the root of all evil
- Abstract common code

### G6: Code at Wrong Level of Abstraction
- Separate higher level concepts from lower level details
- Create appropriate abstraction levels

### G7: Base Classes Depending on Their Derivatives
- Base classes should know nothing about derivatives
- Deploy base and derivatives separately

### G8: Too Much Information
- Modules should have small interfaces
- Hide data, utilities, constants, temporaries
- Limit coupling

### G9: Dead Code
- Code that isn't executed
- Delete it immediately
- Includes: unreachable code, unused functions, unused variables

### G10: Vertical Separation
- Variables and functions should be close to where they're used
- Local variables at top of functions
- Private functions just below first usage

### G11: Inconsistency
- Be consistent with naming, structure, conventions
- If you do something a certain way, always do it that way

### G12: Clutter
- Remove unused variables, functions, comments
- Keep source files clean

### G13: Artificial Coupling
- Don't couple things that don't depend on each other
- Keep coupling natural and obvious

### G14: Feature Envy
- Methods should be interested in their own class
- Not envious of other classes' variables and methods

### G15: Selector Arguments
- Avoid boolean, enum, or integer arguments that select function behavior
- Create separate functions instead

### G16: Obscured Intent
- Code should be expressive
- Avoid clever code that's hard to understand

### G17: Misplaced Responsibility
- Code should be placed where readers expect it
- Follow the Principle of Least Surprise

### G18: Inappropriate Static
- Prefer non-static methods
- Use static only when necessary

### G19: Use Explanatory Variables
- Break calculations into intermediate values with meaningful names
- Make code self-documenting

### G20: Function Names Should Say What They Do
- Names should clearly indicate what the function does
- If you have to look at implementation, name is wrong

### G21: Understand the Algorithm
- Don't just make it work
- Understand WHY it works
- Refactor until the how is obvious

### G22: Make Logical Dependencies Physical
- Dependent modules should explicitly ask for dependencies
- Don't assume indirect dependencies

### G23: Prefer Polymorphism to If/Else or Switch/Case
- Use polymorphism for type-based behavior
- Switch statements should appear only once

### G24: Follow Standard Conventions
- Every team should follow a coding standard
- Consistency is key

### G25: Replace Magic Numbers with Named Constants
- Don't use raw numbers in code
- Hide them behind named constants

### G26: Be Precise
- Don't be lazy with precision
- Handle all cases, test boundaries
- Be specific about types and error handling

### G27: Structure over Convention
- Enforce design decisions with structure
- Don't rely on convention alone

### G28: Encapsulate Conditionals
- Extract complex conditionals into well-named functions
- `if (shouldBeDeleted(timer))` better than `if (timer.hasExpired() && !timer.isRecurrent())`

### G29: Avoid Negative Conditionals
- Positive conditionals are easier to understand
- `if (buffer.shouldCompact())` better than `if (!buffer.shouldNotCompact())`

### G30: Functions Should Do One Thing
- Functions that do one thing cannot be reasonably divided
- Multiple sections indicate multiple responsibilities

### G31: Hidden Temporal Couplings
- Make temporal couplings explicit
- Use arguments to enforce ordering when necessary

### G32: Don't Be Arbitrary
- Have a reason for structure and decisions
- Code structure should communicate reason

### G33: Encapsulate Boundary Conditions
- Boundary conditions are hard to track
- Put processing in one place
- Don't let `+1` and `-1` leak out

### G34: Functions Should Descend Only One Level of Abstraction
- Statements in function should be at same abstraction level
- That level should be one below function name

### G35: Keep Configurable Data at High Levels
- Configuration constants should be at high level
- Easy to find and change
- Pass down to lower levels

### G36: Avoid Transitive Navigation (Law of Demeter)
- Modules should only know immediate collaborators
- Avoid `a.getB().getC().doSomething()`

## Names

### N1: Choose Descriptive Names
- Names should clearly express intent
- Long descriptive names better than short unclear ones

### N2: Choose Names at the Appropriate Level of Abstraction
- Don't use implementation details in names
- Use names that work at the abstraction level

### N3: Use Standard Nomenclature Where Possible
- Use industry-standard terms
- Use pattern names when implementing patterns

### N4: Unambiguous Names
- Names should be clear and unambiguous
- Avoid names that could mean multiple things

### N5: Use Long Names for Long Scopes
- Short names fine for short scopes
- Longer scopes need longer, more descriptive names

### N6: Avoid Encodings
- Don't encode type or scope information
- Names should be pronounceable

### N7: Names Should Describe Side-Effects
- If function has side-effects, name should indicate
- `getOrCreate` better than just `get` if it creates

## Tests

### T1: Insufficient Tests
- Test everything that could possibly break
- Test coverage should be high

### T2: Use a Coverage Tool
- Coverage tools help find untested code
- Aim for 100% coverage (though not always possible)

### T3: Don't Skip Trivial Tests
- Trivial tests are documentation
- Easy to write, high value

### T4: An Ignored Test Is a Question about an Ambiguity
- Don't comment out tests
- Use `@Ignore` with explanation

### T5: Test Boundary Conditions
- Bugs often hide at boundaries
- Test all boundary conditions

### T6: Exhaustively Test Near Bugs
- Bugs tend to congregate
- When you find one, look for others nearby

### T7: Patterns of Failure Are Revealing
- Complete test failure patterns
- Can diagnose problems from pattern

### T8: Test Coverage Patterns Can Be Revealing
- Look at code that's executed or not by failing tests
- Gives clues about why tests fail

### T9: Tests Should Be Fast
- Slow tests won't be run
- Do whatever necessary to keep tests fast

## Summary of Key Principles

### Code Quality Indicators

**Good Code:**
- Reads like well-written prose
- Makes intent clear
- Minimizes surprises
- Has high test coverage
- Contains no duplication

**Bad Code (Smells):**
- Long functions and classes
- Duplicate code
- Dead code
- Inconsistent conventions
- Hidden dependencies

### How to Apply

1. **Write code that works**
2. **Refactor mercilessly**
3. **Apply these heuristics**
4. **Keep tests green**
5. **Never stop improving**

### Remember

- These are heuristics, not rules
- Use judgment in application
- Context matters
- Consistency is crucial
- Continuous improvement is key

**The goal is not to follow all rules blindly, but to write code that is clean, maintainable, and expresses intent clearly.**