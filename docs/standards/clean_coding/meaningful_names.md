# Meaningful Names

## Agent Summary

**Quick Rules for Naming:**
- Names must reveal intent (why it exists, what it does, how it's used)
- No mental mapping required - be explicit
- Classes: nouns (`Customer`, `Account`)
- Methods: verbs (`save`, `deletePage`)
- One word per concept across codebase
- No noise words (`Info`, `Data`, `Manager`)
- Searchable and pronounceable
- No type encoding or prefixes

**Key Actions:**
1. Replace cryptic names with descriptive ones
2. Remove redundant words from names
3. Use domain/solution vocabulary appropriately
4. Keep context minimal but clear
5. Ensure consistency across the codebase

---

Names are everywhere in software - files, directories, variables, functions, classes. Since we do so much naming, we should do it well.

## Use Intention-Revealing Names

The name of a variable, function or class should answer all the big questions:
- Why it exists
- What it does  
- How it is used

**If a name requires a comment, then the name does not reveal its intent.**

### Bad Example
```java
int d; // elapsed time in days
```

### Good Example
```java
int elapsedTimeInDays;
```

### Complex Example - Bad
```java
public List<int[]> getThem() {
  List<int[]> list1 = new ArrayList<int[]>();
  for (int[] x : theList)
    if (x[0] == 4)
      list1.add(x);
  return list1;
}
```

This raises questions:
- What is in `theList`?
- What is the significance of `x[0]`?
- Why compare to `4`?
- How is the returned list used?

### Complex Example - Good
```java
public List<Cell> getFlaggedCells() {
  List<Cell> flaggedCells = new ArrayList<Cell>();
  for (Cell cell : gameBoard)
    if (cell.isFlagged())
      flaggedCells.add(cell);
  return flaggedCells;
}
```

## Avoid Disinformation

- Don't use words whose entrenched meanings vary from intended meaning
- Avoid using `accountList` unless it's actually a `List` - use `accountGroup`, `bunchOfAccounts`, or `accounts` instead
- Beware of names which vary in small ways: `XYZControllerForEfficientHandlingOfStrings` vs `XYZControllerForEfficientStorageOfStrings`

## Make Meaningful Distinctions

- Don't use arbitrary name changes just to satisfy the compiler
- Avoid number-series naming: `a1`, `a2`, etc.
- Avoid noise words: `ProductInfo` and `ProductData` are indistinct
- The word `variable` should never appear in a variable name
- The word `table` should never appear in a table name

### Bad Example
```java
public static void copyChars(char a1[], char a2[]) {
  for (int i = 0; i < a1.length; i++) {
    a2[i] = a1[i];
  }
}
```

### Good Example
```java
public static void copyChars(char source[], char destination[]) {
  for (int i = 0; i < source.length; i++) {
    destination[i] = source[i];
  }
}
```

## Use Pronounceable Names

Avoid cryptic abbreviations. You should be able to pronounce names in conversation.

### Bad Example
```java
class DtaRcrd102 {
  private Date genymdhms; // generation date, year, month, day, hour, minute, second
  private Date modymdhms;
  private final String pszqint = "102";
}
```

### Good Example
```java
class Customer {
  private Date generationTimestamp;
  private Date modificationTimestamp;
  private final String recordId = "102";
}
```

## Use Searchable Names

- Single-letter names and numeric constants are hard to find in text
- Single-letter names should ONLY be used as local variables inside short methods

## Avoid Encoding

- Don't encode type or scope information into names
- Avoid Hungarian Notation
- Avoid member prefixes

### Interfaces and Implementations

- Prefer unadorned interfaces: `ShapeFactory` rather than `IShapeFactory`
- If you must encode, encode the implementation: `ShapeFactoryImp` or `CShapeFactory`

## Avoid Mental Mapping

- Readers shouldn't have to mentally translate names
- Clarity is king - professionals write code that others can understand

## Class Names

- Should be **nouns or noun phrases**
- Examples: `Customer`, `WikiPage`, `Account`, `AddressParser`
- Avoid: `Manager`, `Processor`, `Data`, `Info`
- **A class name should not be a verb**

## Method Names

- Should be **verbs or verb phrases**
- Examples: `postPayment`, `deletePage`, `save`
- Accessors, mutators, predicates: prefix with `get`, `set`, `is`

### Static Factory Methods
```java
Complex fulcrumPoint = Complex.FromRealNumber(23.0);
// Better than:
Complex fulcrumPoint = new Complex(23.0);
```

## Don't Be Cute

| Cute name | Clean name |
|-----------|------------|
| `holyHandGrenade` | `deleteItems` |
| `whack` | `kill` |
| `eatMyShorts` | `abort` |

## Pick One Word per Concept

- Don't use `fetch`, `retrieve`, and `get` for equivalent methods
- Be consistent across the codebase

## Don't Pun

- Avoid using the same word for two different purposes
- If `add` creates a new value by combining two values in one class, use `insert` or `append` in another class where you're adding to a collection

## Use Solution Domain Names

- Use CS terms, algorithm names, pattern names, math terms
- Programmers will read your code - use their vocabulary

## Use Problem Domain Names

- When there's no programmer term available, use the problem domain name
- At least maintainers can ask domain experts

## Add Meaningful Context

- Variables like `firstName`, `lastName`, `street`, `city`, `state` are clear together
- Alone, `state` might be ambiguous
- Better: create an `Address` class

## Don't Add Gratuitous Context

- In "Gas Station Deluxe" app, don't prefix everything with `GSD`
- Shorter names are better when they're clear
- Add no more context than necessary