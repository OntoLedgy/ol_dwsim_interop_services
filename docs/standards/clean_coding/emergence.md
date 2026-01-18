# Emergence

## Agent Summary

**Kent Beck's Simple Design Rules (in order):**
1. Runs all tests
2. No duplication
3. Expresses intent
4. Minimal classes/methods

**Key Actions:**
1. Write tests first (drives good design)
2. Remove all duplication
3. Refactor for clarity after tests pass
4. Use descriptive names
5. Don't over-engineer (minimize but don't go overboard)

---

Getting clean via emergent design. According to Kent Beck, a design is "simple" if it follows these rules (in order of importance):

## Kent Beck's Four Rules of Simple Design

### 1. Runs All the Tests

A system that cannot be verified should never be deployed.

- Tests verify that the system behaves as intended
- Making systems testable pushes toward better design
- Testable systems have better SRP and DIP adherence
- Writing tests leads to better designs

**Tight coupling makes testing difficult.** The more tests we write, the more we use principles like DIP and tools like dependency injection, interfaces, and abstraction to minimize coupling.

### 2. Contains No Duplication

Duplication is the primary enemy of a well-designed system.

- Duplication represents additional work
- Duplication represents additional risk
- Duplication represents unnecessary complexity

#### Example - Removing Duplication

```java
// Before - Duplication
public void scaleToOneDimension(float desiredDimension, float imageDimension) {
  if (Math.abs(desiredDimension - imageDimension) < errorThreshold)
    return;
  float scalingFactor = desiredDimension / imageDimension;
  scalingFactor = (float)(Math.floor(scalingFactor * 100) * 0.01f);
  
  RenderedOp newImage = ImageUtilities.getScaledImage(image, scalingFactor, scalingFactor);
  image.dispose();
  System.gc();
  image = newImage;
}

public synchronized void rotate(int degrees) {
  RenderedOp newImage = ImageUtilities.getRotatedImage(image, degrees);
  image.dispose();
  System.gc();
  image = newImage;
}
```

```java
// After - No Duplication
public void scaleToOneDimension(float desiredDimension, float imageDimension) {
  if (Math.abs(desiredDimension - imageDimension) < errorThreshold)
    return;
  float scalingFactor = desiredDimension / imageDimension;
  scalingFactor = (float)(Math.floor(scalingFactor * 100) * 0.01f);
  
  replaceImage(ImageUtilities.getScaledImage(image, scalingFactor, scalingFactor));
}

public synchronized void rotate(int degrees) {
  replaceImage(ImageUtilities.getRotatedImage(image, degrees));
}

private void replaceImage(RenderedOp newImage) {
  image.dispose();
  System.gc();
  image = newImage;
}
```

#### The Template Method Pattern

Another technique for removing duplication:

```java
public abstract class VacationPolicy {
  public void accrueVacation() {
    calculateBaseVacationHours();
    alterForLegalMinimums();
    applyToPayroll();
  }
  
  protected abstract void calculateBaseVacationHours();
  protected abstract void alterForLegalMinimums();
  
  private void applyToPayroll() {
    // Common implementation
  }
}

public class USVacationPolicy extends VacationPolicy {
  protected void calculateBaseVacationHours() {
    // US specific calculation
  }
  
  protected void alterForLegalMinimums() {
    // US specific legal requirements
  }
}

public class EUVacationPolicy extends VacationPolicy {
  protected void calculateBaseVacationHours() {
    // EU specific calculation
  }
  
  protected void alterForLegalMinimums() {
    // EU specific legal requirements
  }
}
```

### 3. Expresses the Intent of the Programmer

Code should clearly express the intent of its author.

#### How to Express Yourself in Code

1. **Choose good names** - Take time to select names that express intent
2. **Keep functions and classes small** - Small things are easier to name and understand
3. **Use standard nomenclature** - Use design pattern names when applicable
4. **Write good unit tests** - Tests document the intended behavior
5. **Try different ways** - Refactor to find the clearest expression

#### Example - Expressing Intent

```java
// Poor expression
public int x() {
  int q = 0;
  int z = 0;
  for (int kk = 0; kk < 10; kk++) {
    if (l[z] == 10) {
      q += 10 + (l[z + 1] + l[z + 2]);
      z += 1;
    }
    else if (l[z] + l[z + 1] == 10) {
      q += 10 + l[z + 2];
      z += 2;
    }
    else {
      q += l[z] + l[z + 1];
      z += 2;
    }
  }
  return q;
}
```

```java
// Good expression
public int score() {
  int score = 0;
  int frame = 0;
  for (int frameNumber = 0; frameNumber < 10; frameNumber++) {
    if (isStrike(frame)) {
      score += 10 + nextTwoBallsForStrike(frame);
      frame += 1;
    }
    else if (isSpare(frame)) {
      score += 10 + nextBallForSpare(frame);
      frame += 2;
    }
    else {
      score += twoBallsInFrame(frame);
      frame += 2;
    }
  }
  return score;
}

private boolean isStrike(int frame) {
  return rolls[frame] == 10;
}

private boolean isSpare(int frame) {
  return rolls[frame] + rolls[frame + 1] == 10;
}

private int nextTwoBallsForStrike(int frame) {
  return rolls[frame + 1] + rolls[frame + 2];
}

private int nextBallForSpare(int frame) {
  return rolls[frame + 2];
}

private int twoBallsInFrame(int frame) {
  return rolls[frame] + rolls[frame + 1];
}
```

### 4. Minimizes the Number of Classes and Methods

Keep class and method counts low, but this is the LOWEST priority rule.

- Don't create too many tiny classes and methods
- Balance is important
- This rule is less important than the other three
- Pragmatism is important

#### Avoid Dogma

Creating classes and methods to satisfy arbitrary metrics is pointless:
- Don't create interfaces for every class
- Don't split every function into tiny pieces
- Be practical

**High class and method counts are sometimes the result of pointless dogmatism.**

## The Process of Emergence

Following the rules in order creates emergent design:

1. **Run all tests** - Forces testable, decoupled design
2. **Refactor** - After tests pass, clean up the code
3. **Eliminate duplication** - Extract common code
4. **Express intent** - Make code clear
5. **Minimize classes/methods** - But don't go overboard

### The Cycle

1. Make it work (tests pass)
2. Make it right (refactor)
3. Make it small (minimize)

During refactoring:
- Increase cohesion
- Decrease coupling
- Separate concerns
- Modularize system concerns
- Shrink functions and classes
- Choose better names

**Do this continuously, not just once.**

## Example: Applying Simple Design

Starting with working but messy code:

```java
// Initial working version
public class Args {
  private String schema;
  private String[] args;
  private boolean valid;
  private Set<Character> unexpectedArguments = new TreeSet<Character>();
  private Map<Character, Boolean> booleanArgs = new HashMap<Character, Boolean>();
  private Map<Character, String> stringArgs = new HashMap<Character, String>();
  private Map<Character, Integer> intArgs = new HashMap<Character, Integer>();
  private Set<Character> argsFound = new HashSet<Character>();
  private int currentArgument;
  private char errorArgument = '\0';
  
  // ... lots of complex parsing code
}
```

After applying simple design:

```java
// After refactoring
public class Args {
  private Map<Character, ArgumentMarshaler> marshalers;
  private Set<Character> argsFound;
  private ListIterator<String> currentArgument;
  
  public Args(String schema, String[] args) throws ArgsException {
    marshalers = new HashMap<Character, ArgumentMarshaler>();
    argsFound = new HashSet<Character>();
    parseSchema(schema);
    parseArgumentStrings(Arrays.asList(args));
  }
  
  private void parseSchema(String schema) throws ArgsException {
    for (String element : schema.split(","))
      if (element.length() > 0)
        parseSchemaElement(element.trim());
  }
  
  private void parseSchemaElement(String element) throws ArgsException {
    char elementId = element.charAt(0);
    String elementTail = element.substring(1);
    validateSchemaElementId(elementId);
    if (elementTail.length() == 0)
      marshalers.put(elementId, new BooleanArgumentMarshaler());
    else if (elementTail.equals("*"))
      marshalers.put(elementId, new StringArgumentMarshaler());
    else if (elementTail.equals("#"))
      marshalers.put(elementId, new IntegerArgumentMarshaler());
    else
      throw new ArgsException(INVALID_ARGUMENT_FORMAT, elementId, elementTail);
  }
  // ... clean, simple methods
}
```

## Summary

### The Rules (in order)

1. **Runs all the tests** - Most important
2. **Contains no duplication** - Critical for maintenance
3. **Expresses intent** - Makes code readable
4. **Minimizes classes and methods** - But don't overdo it

### Key Practices

- Follow the rules in order
- Refactor continuously
- Keep the code simple
- Don't be dogmatic
- Be pragmatic

### Benefits

- Clean code emerges from simple rules
- Design improves incrementally
- Code stays maintainable
- System remains flexible

**Simple design is not a one-time thing. It emerges from the continuous application of these rules during the development process.**