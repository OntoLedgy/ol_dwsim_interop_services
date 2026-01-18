# Unit Tests

## Agent Summary

**Quick Rules (F.I.R.S.T.):**
- Fast: Tests run quickly
- Independent: No dependencies between tests
- Repeatable: Same results every time
- Self-Validating: Pass/fail, no manual checking
- Timely: Written just before production code

**Key Actions:**
1. One concept per test
2. Keep tests as clean as production code
3. Use domain-specific test language
4. Follow Build-Operate-Check pattern
5. Maintain high test coverage
6. Never ignore failing tests

---

Test Driven Development (TDD) has become a cornerstone of professional software development.

## The Three Laws of TDD

1. **First Law:** You may not write production code until you have written a failing unit test.
2. **Second Law:** You may not write more of a unit test than is sufficient to fail (not compiling is failing).
3. **Third Law:** You may not write more production code than is sufficient to pass the currently failing test.

These laws lock you into a cycle that is perhaps thirty seconds long. The tests and production code are written together, with tests just a few seconds ahead of the production code.

## Keeping Tests Clean

**Test code is just as important as production code.**

- It requires thought, design, and care
- It must be kept as clean as production code
- It requires maintenance as the production code evolves

### Why Clean Tests Matter

Without clean tests:
- Tests become harder to change
- Production code becomes harder to change
- Tests get abandoned
- Production code rots without test coverage
- Defects rise
- Code becomes unmaintainable

**The dirtier the tests, the dirtier the code becomes.**

## What Makes a Clean Test?

**Three things: Readability, readability, and readability.**

### Build-Operate-Check Pattern

Tests should follow a simple pattern:
1. **Build:** Create test data
2. **Operate:** Execute the operation
3. **Check:** Verify the results

### Bad Test Example
```java
public void testGetPageHieratchyAsXml() throws Exception {
  crawler.addPage(root, PathParser.parse("PageOne"));
  crawler.addPage(root, PathParser.parse("PageOne.ChildOne"));
  crawler.addPage(root, PathParser.parse("PageTwo"));
  
  request.setResource("root");
  request.addInput("type", "pages");
  Responder responder = new SerializedPageResponder();
  SimpleResponse response = 
    (SimpleResponse) responder.makeResponse(
      new FitNesseContext(root), request);
  String xml = response.getContent();
  
  assertEquals("text/xml", response.getContentType());
  assertSubString("<name>PageOne</name>", xml);
  assertSubString("<name>PageTwo</name>", xml);
  assertSubString("<name>ChildOne</name>", xml);
}
```

### Good Test Example
```java
public void testGetPageHierarchyAsXml() throws Exception {
  makePages("PageOne", "PageOne.ChildOne", "PageTwo");
  
  submitRequest("root", "type:pages");
  
  assertResponseIsXML();
  assertResponseContains(
    "<name>PageOne</name>", 
    "<name>PageTwo</name>",
    "<name>ChildOne</name>"
  );
}
```

## Domain-Specific Testing Language

Build a set of functions and utilities that make tests more convenient to write and easier to read.

```java
public void testSymbolicLinksAreNotFollowedByDefault() throws Exception {
  givenPages("PageOne", "PageOne.ChildOne", "PageTwo");
  givenSymlink("PageTwo", "PageOne.ChildOne", "SymPage");
  
  whenRequestIsIssued("root", "type:pages");
  
  thenResponseShouldBeXML();
  thenResponseShouldContain(
    "<name>PageOne</name>",
    "<name>PageTwo</name>", 
    "<name>ChildOne</name>"
  );
  thenResponseShouldNotContain("SymPage");
}
```

## A Dual Standard

Test code should be:
- Simple
- Succinct  
- Expressive

But it doesn't need to be as efficient as production code. Tests run in a test environment, not production.

### Example: Environmental Control System

Production code (embedded system):
```java
@Test
public void turnOnLoTempAlarmAtThreshold() throws Exception {
  hw.setTemp(WAY_TOO_COLD);
  controller.tic();
  assertTrue(hw.heaterState());
  assertTrue(hw.blowerState());
  assertFalse(hw.coolerState());
  assertFalse(hw.hiTempAlarm());
  assertTrue(hw.loTempAlarm());
}
```

Better test code:
```java
@Test
public void turnOnLoTempAlarmAtThreshold() throws Exception {
  wayTooCold();
  assertEquals("HBchL", hw.getState());
}
```

Where state string means:
- {heater, blower, cooler, hi-temp-alarm, lo-temp-alarm}
- Capital = on, lowercase = off

## One Assert per Test

This rule helps keep tests focused and easy to understand.

### Multiple Asserts Example
```java
@Test
public void testAbcde() throws Exception {
  givenSomeSetup();
  
  whenSomethingHappens();
  
  assertEquals(expected1, actual1);
  assertEquals(expected2, actual2);
  assertEquals(expected3, actual3);
}
```

### Single Assert per Test
```java
@Test
public void testA() throws Exception {
  givenSomeSetup();
  whenSomethingHappens();
  assertEquals(expectedA, actualA);
}

@Test  
public void testB() throws Exception {
  givenSomeSetup();
  whenSomethingHappens();
  assertEquals(expectedB, actualB);
}
```

### TEMPLATE METHOD Pattern for Reducing Duplication
```java
public class TestWithTemplate {
  @Before
  public void setUp() throws Exception {
    givenSomeSetup();
    whenSomethingHappens();
  }
  
  @Test
  public void testA() throws Exception {
    assertEquals(expectedA, actualA);
  }
  
  @Test
  public void testB() throws Exception {
    assertEquals(expectedB, actualB);
  }
}
```

## Single Concept per Test

A better rule than one assert per test: **Test a single concept in each test function.**

```java
// Bad - Testing multiple concepts
public void testAddMonths() {
  // Test month boundary
  Date date1 = SerialDate.createInstance(31, 5, 2004);
  Date date2 = date1.addMonths(1);
  assertEquals(30, date2.getDayOfMonth());
  assertEquals(6, date2.getMonth());
  
  // Test year boundary
  Date date3 = SerialDate.createInstance(31, 1, 2004);
  Date date4 = date3.addMonths(1);
  assertEquals(29, date4.getDayOfMonth());
  assertEquals(2, date4.getMonth());
  
  // Test leap year
  Date date5 = SerialDate.createInstance(29, 2, 2004);
  Date date6 = date5.addMonths(12);
  assertEquals(28, date6.getDayOfMonth());
  assertEquals(2, date6.getMonth());
}
```

Better - Separate concepts:
```java
public void testAddMonthsWrapToNextMonth() {
  Date may31 = SerialDate.createInstance(31, 5, 2004);
  Date june30 = may31.addMonths(1);
  assertEquals(30, june30.getDayOfMonth());
  assertEquals(6, june30.getMonth());
}

public void testAddMonthsWrapToFebruary() {
  Date jan31 = SerialDate.createInstance(31, 1, 2004);
  Date feb29 = jan31.addMonths(1);
  assertEquals(29, feb29.getDayOfMonth());
  assertEquals(2, feb29.getMonth());
}

public void testLeapYearEndingOnFebruary29() {
  Date feb29_2004 = SerialDate.createInstance(29, 2, 2004);
  Date feb28_2005 = feb29_2004.addMonths(12);
  assertEquals(28, feb28_2005.getDayOfMonth());
  assertEquals(2, feb28_2005.getMonth());
}
```

## F.I.R.S.T.

Clean tests follow these five rules:

### Fast
- Tests should run quickly
- Slow tests won't get run frequently
- Problems won't be detected early

### Independent
- Tests should not depend on each other
- One test should not set up conditions for the next
- Tests should be runnable in any order

### Repeatable
- Tests should be repeatable in any environment
- Production, QA, local laptop, without network
- No excuses for test failures

### Self-Validating
- Tests should have boolean output - pass or fail
- Should not require manual evaluation
- No log file comparisons or manual checks

### Timely
- Tests should be written just before production code
- Hard to test code written without tests in mind
- May not be testable at all

## Test Coverage

- Use coverage tools to ensure tests execute all code paths
- Don't skip trivial tests - they provide documentation
- Test boundary conditions rigorously
- Exhaustively test near bugs - bugs congregate
- Patterns of failure are revealing
- Test coverage patterns can be revealing

## Summary

### Key Principles

1. **Test code is as important as production code**
2. **Keep tests clean and readable**
3. **Use domain-specific test language**
4. **One concept per test**
5. **Follow F.I.R.S.T. principles**
6. **Maintain high test coverage**
7. **Tests enable change**

Tests are what keep your production code flexible, maintainable, and reusable. Without tests, every change is a potential bug. With tests, you can change code without fear.

**Tests are documentation** - Unit tests describe how the production code is supposed to work in unambiguous detail.

**If you let the tests rot, then the code will rot too.**