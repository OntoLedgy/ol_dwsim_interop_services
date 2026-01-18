# Comments

## Agent Summary

**Quick Rules for Comments:**
- Comments are failures - code should be self-explanatory
- Delete commented-out code immediately
- Update/delete obsolete comments
- Only write comments when absolutely necessary
- Express yourself in code, not comments

**Acceptable Comments:**
- Legal/copyright notices
- TODO comments (with clear actions)
- Public API documentation
- Warning of consequences
- Explanation of complex algorithms (rare)

**Key Actions:**
1. Replace comments with descriptive names/functions
2. Delete all commented-out code
3. Remove redundant/obvious comments
4. Extract complex conditions into named functions
5. Make code self-documenting

---

Nothing can be quite so helpful as a well-placed comment. Nothing can clutter up a module more than frivolous dogmatic comments. Nothing can be quite so damaging as an old comment that propagates lies and misinformation.

## The Truth

**The proper use of comments is to compensate for our failure to express ourselves in code.**

Comments are always failures. We must have them because we cannot always figure out how to express ourselves without them, but their use is not a cause for celebration.

## Comments Do Not Make Up for Bad Code

Clear and expressive code with few comments is far superior to cluttered and complex code with lots of comments.

**Rather than spend time writing comments that explain messy code, spend it cleaning the code.**

## Explain Yourself in Code

Bad:
```java
// Check to see if the employee is eligible for full benefits
if ((employee.flags & HOURLY_FLAG) && (employee.age > 65))
```

Good:
```java
if (employee.isEligibleForFullBenefits())
```

## Good Comments

Some comments are necessary or beneficial. The only truly good comment is the comment you found a way not to write.

### Legal Comments

Copyright and authorship statements required by corporate standards.

### Informative Comments

```java
// Returns an instance of the Responder being tested.
protected abstract Responder responderInstance();
```

Better: rename to `responderBeingTested()`

### Explanation of Intent

Explains WHY, not WHAT:

```java
return 1; // we are greater because we are the right type.
```

### Clarification

When code uses standard libraries or unchangeable code:

```java
assertTrue(a.compareTo(b) != 0); // a != b
```

### Warning of Consequences

```java
// Don't run unless you have some time to kill.
public void _testWithReallyBigFile() {
```

### TODO Comments

Jobs that should be done but can't be done now:

```java
//TODO-MdM these are not needed
// We expect this to go away when we do the checkout model
protected VersionInfo makeVersion() throws Exception {
  return null;
}
```

**TODOs are not an excuse to leave bad code.**

### Amplification

Amplify importance of something seemingly inconsequential:

```java
String listItemContent = match.group(3).trim();
// the trim is real important. It removes the starting
// spaces that could cause the item to be recognized
// as another list.
```

### Javadocs in Public APIs

Well-described public APIs are invaluable. The Javadocs for standard Java library are exemplary.

## Bad Comments

Most comments fall into this category.

### Mumbling

Comments written just because you feel you should:

```java
catch(IOException e) {
  // No properties files means all defaults are loaded
}
```

What loads the defaults? This comment raises more questions than it answers.

### Redundant Comments

Comments that say exactly what the code says:

```java
// Utility method that returns when this.closed is true.
// Throws an exception if the timeout is reached.
public synchronized void waitForClose(final long timeoutMillis) throws Exception {
  if(!closed) {
    wait(timeoutMillis);
    if(!closed)
      throw new Exception("MockResponseSender could not be closed");
  }
}
```

The comment is less precise and less informative than the code.

### Misleading Comments

Comments that aren't precise enough to be accurate. The method above doesn't return when `this.closed` becomes `true`; it returns if `this.closed` is `true`.

### Mandated Comments

Rules that every function must have a Javadoc are silly:

```java
/**
 * @param title The title of the CD
 * @param author The author of the CD
 * @param tracks The number of tracks on the CD
 * @param durationInMinutes The duration of the CD in minutes
 */
public void addCD(String title, String author, int tracks, int durationInMinutes) {
```

### Journal Comments

```java
* Changes (from 11-Oct-2001)
* --------------------------
* 11-Oct-2001 : Re-organised the class...
```

We have source control systems now.

### Noise Comments

```java
/**
 * Default constructor.
 */
protected AnnualDateRule() {
}

/** The day of the month. */
private int dayOfMonth;
```

These add nothing.

### Don't Use a Comment When You Can Use a Function or Variable

Bad:
```java
// does the module from the global list <mod> depend on the
// subsystem we are part of?
if (smodule.getDependSubsystems().contains(subSysMod.getSubSystem()))
```

Good:
```java
ArrayList moduleDependees = smodule.getDependSubsystems();
String ourSubSystem = subSysMod.getSubSystem();
if (moduleDependees.contains(ourSubSystem))
```

### Position Markers

```java
// Actions //////////////////////////////////
```

Rarely useful, mostly clutter.

### Closing Brace Comments

```java
} //while
} // try
} //catch
```

If you need these, your functions are too long. Shorten them instead.

### Attributions and Bylines

```java
/* Added by Rick */
```

Source control handles this.

### Commented-Out Code

```java
// InputStream resultsStream = formatter.getResultStream();
// StreamReader reader = new StreamReader(resultsStream);
```

**Delete it!** Source control remembers it.

### HTML Comments

HTML in source code comments is an abomination.

### Nonlocal Information

Don't offer systemwide information in a local comment.

### Too Much Information

Don't put historical discussions or irrelevant details in comments.

### Inobvious Connection

The connection between comment and code should be obvious.

### Function Headers

Short functions don't need much description. A well-chosen name is usually better.

### Javadocs in Nonpublic Code

Generating Javadoc pages for nonpublic code is not generally useful.

## Summary

**Comments are a necessary evil at best.** 

Spend energy making code express itself clearly rather than writing comments. When you must write comments, take time to make them the best they can be: clear, correct, and genuinely helpful.