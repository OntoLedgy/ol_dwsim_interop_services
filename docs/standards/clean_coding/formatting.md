# Formatting

## Agent Summary

**Quick Rules for Formatting:**
- Vertical: Separate concepts with blank lines
- Keep related code close together
- Functions flow downward (high to low level)
- Lines < 120 characters
- Consistent indentation (no breaking for short statements)
- Team rules override personal preference

**Key Actions:**
1. Add blank lines between concepts
2. Move variables close to usage
3. Order functions by call dependency (caller above callee)
4. Remove unnecessary alignment
5. Apply team formatting standards consistently

---

Code formatting is about communication, and communication is the professional developer's first order of business.

## Vertical Formatting

### Vertical Openness Between Concepts

Use blank lines to separate concepts. Each blank line is a visual cue that identifies a new and separate concept.

#### Good Example
```java
package fitnesse.wikitext.widgets;

import java.util.regex.*;

public class BoldWidget extends ParentWidget {
  public static final String REGEXP = "'''.+?'''";
  private static final Pattern pattern = Pattern.compile("'''(.+?)'''",
      Pattern.MULTILINE + Pattern.DOTALL);

  public BoldWidget(ParentWidget parent, String text) throws Exception {
    super(parent);
    Matcher match = pattern.matcher(text);
    match.find();
    addChildWidgets(match.group(1));
  }

  public String render() throws Exception {
    StringBuffer html = new StringBuffer("<b>");
    html.append(childHtml()).append("</b>");
    return html.toString();
  }
}
```

### Vertical Density

Lines of code that are tightly related should appear vertically dense.

#### Bad Example
```java
public class ReporterConfig {

  /**
   * The class name of the reporter listener
   */
  private String m_className;

  /**
   * The properties of the reporter listener
   */
  private List<Property> m_properties = new ArrayList<Property>();

  public void addProperty(Property property) {
    m_properties.add(property);
  }
}
```

#### Good Example
```java
public class ReporterConfig {
  private String m_className;
  private List<Property> m_properties = new ArrayList<Property>();

  public void addProperty(Property property) {
    m_properties.add(property);
  }
}
```

### Vertical Distance

Concepts that are closely related should be kept vertically close to each other.

#### Variable Declarations
- Declare variables as close to their usage as possible
- Local variables should appear at the top of functions

#### Instance Variables
- Should be declared at the top of the class
- Everyone should know where to find them
- Don't scatter them throughout the class

#### Dependent Functions
- If one function calls another, they should be vertically close
- The caller should be above the callee when possible
- Creates natural flow down the source code

#### Conceptual Affinity
- Code with strong conceptual affinity should be close
- Affinity causes:
  - One function calling another
  - Functions performing similar operations
  - Functions sharing naming scheme

### Vertical Ordering

**Function call dependencies should point downward.**

- High-level functions at top
- Low-level details below
- Read code from top to bottom like a newspaper article

## Horizontal Formatting

### Line Length

- Strive for lines shorter than 120 characters
- Lines beyond 100-120 characters are careless
- 80 characters is a reasonable limit

### Horizontal Openness and Density

Use horizontal white space to associate strongly related things and disassociate weakly related things.

```java
private void measureLine(String line) {
  lineCount++;
  int lineSize = line.length();
  totalChars += lineSize;
  lineWidthHistogram.addLine(lineSize, lineCount);
  recordWidestLine(lineSize);
}
```

Notice:
- Spaces around assignment operators
- No spaces between function names and parentheses
- Spaces after commas in arguments

### Horizontal Alignment

#### Bad (Misleading Alignment)
```java
public class Example implements Base {
  private   Socket      socket;
  private   InputStream input;
  protected long        requestProgress;

  public Expediter(Socket      s,
                   InputStream input) {
    this.socket =     s;
    this.input  =     input;
  }
}
```

#### Good (No Alignment)
```java
public class Example implements Base {
  private Socket socket;
  private InputStream input;
  protected long requestProgress;

  public Expediter(Socket s, InputStream input) {
    this.socket = s;
    this.input = input;
  }
}
```

Alignment emphasizes the wrong things and leads eyes away from true intent.

### Indentation

- Makes hierarchy visible
- Shows scope at a glance
- Without indentation, programs are nearly incomprehensible

#### Indentation Rules

- Class members indented one level from class
- Methods indented one level from class
- Blocks indented one level from containing block
- Don't break indentation for short statements:

```java
// Bad
public class CommentWidget extends TextWidget {
  public CommentWidget(String text) { super(text); }
}

// Good
public class CommentWidget extends TextWidget {
  public CommentWidget(String text) {
    super(text);
  }
}
```

### Dummy Scopes

Avoid dummy scopes (empty while/for bodies). If unavoidable, make the emptiness obvious:

```java
while (dis.read(buf, 0, readBufferSize) != -1)
  ;
```

## Team Rules

**Every programmer has favorite formatting rules, but if working in a team, the team rules.**

- A team should agree on a single formatting style
- Every member must use that style
- Software should have consistent style
- Shouldn't appear to be written by individuals with different styles

### Setting Team Rules

1. Sit down together
2. Work out coding standards
3. Use automated formatting tools
4. Configure IDE formatters identically
5. Create a settings file all can import

## Uncle Bob's Formatting Rules

Example of well-formatted code following all principles:

```java
public class CodeAnalyzer implements JavaFileAnalysis {
  private int lineCount;
  private int maxLineWidth;
  private int widestLineNumber;
  private LineWidthHistogram lineWidthHistogram;
  private int totalChars;

  public CodeAnalyzer() {
    lineWidthHistogram = new LineWidthHistogram();
  }

  public static List<File> findJavaFiles(File parentDirectory) {
    List<File> files = new ArrayList<File>();
    findJavaFiles(parentDirectory, files);
    return files;
  }

  private static void findJavaFiles(File parentDirectory, List<File> files) {
    for (File file : parentDirectory.listFiles()) {
      if (file.getName().endsWith(".java"))
        files.add(file);
      else if (file.isDirectory())
        findJavaFiles(file, files);
    }
  }

  public void analyzeFile(File javaFile) throws Exception {
    BufferedReader br = new BufferedReader(new FileReader(javaFile));
    String line;
    while ((line = br.readLine()) != null)
      measureLine(line);
  }

  private void measureLine(String line) {
    lineCount++;
    int lineSize = line.length();
    totalChars += lineSize;
    lineWidthHistogram.addLine(lineSize, lineCount);
    recordWidestLine(lineSize);
  }

  private void recordWidestLine(int lineSize) {
    if (lineSize > maxLineWidth) {
      maxLineWidth = lineSize;
      widestLineNumber = lineCount;
    }
  }

  public int getLineCount() {
    return lineCount;
  }

  public int getMaxLineWidth() {
    return maxLineWidth;
  }

  public int getWidestLineNumber() {
    return widestLineNumber;
  }

  public LineWidthHistogram getLineWidthHistogram() {
    return lineWidthHistogram;
  }

  public double getMeanLineWidth() {
    return (double)totalChars/lineCount;
  }

  public int getMedianLineWidth() {
    Integer[] sortedWidths = getSortedWidths();
    int cumulativeLineCount = 0;
    for (int width : sortedWidths) {
      cumulativeLineCount += lineCountForWidth(width);
      if (cumulativeLineCount > lineCount/2)
        return width;
    }
    throw new Error("Cannot get here");
  }

  private int lineCountForWidth(int width) {
    return lineWidthHistogram.getLinesforWidth(width).size();
  }

  private Integer[] getSortedWidths() {
    Set<Integer> widths = lineWidthHistogram.getWidths();
    Integer[] sortedWidths = widths.toArray(new Integer[0]);
    Arrays.sort(sortedWidths);
    return sortedWidths;
  }
}
```