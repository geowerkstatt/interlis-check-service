import groupBy from "object.groupby";
import i18n from "./i18n";

const constraintPatterns = [
  ["Mandatory Constraint", /^validate mandatory constraint (\S+)\.\.\.$/],
  ["Plausibility Constraint", /^validate plausibility constraint (\S+)\.\.\.$/],
  ["Existence Constraint", /^validate existence constraint (\S+)\.\.\.$/],
  ["Uniqueness Constraint", /^validate unique constraint (\S+)\.\.\.$/],
  ["Set Constraint", /^validate set constraint (\S+)\.\.\.$/],
];

/**
 * Pattern to extract the custom error message of a constraint from a log entry.
 * Requires verbose logging to be enabled that the qualified name and INTERLIS syntax of the constraint is included.
 * e.g. "Custom message ModelA.TopicA.ClassA.ConstraintName (INTERLIS syntax)" will result in "Custom message"
 */
const customConstraintMessagePattern = /^(.*) (\w+\.\w+\.\w+\.\w+) \(.*\)$/;
/**
 * Pattern to detect a message related to a constraint from a log entry.
 * e.g. " ModelA.TopicA.ClassA.ConstraintName (INTERLIS syntax) " (the INTERLIS syntax is optional and will be removed from the message)
 */
const constraintNamePattern = /\s(\w+\.\w+\.\w+\.\w+)(\s\(.+\))?\s/;

/**
 * Converts the given log entries into a hierarchical structure grouped by model and class.
 * @param {*[]} logEntries Entries of the validator log.
 * @returns {*[]} Hierarchical log entries.
 */
export function createLogHierarchy(logEntries) {
  const logBuilder = new LogHierarchyBuilder(logEntries);
  return logBuilder.buildHierarchicalLogEntries();
}

class LogHierarchyBuilder {
  constraintEntries = new Map();
  otherErrorMessages = new Set();
  otherWarningMessages = new Set();

  constructor(logEntries) {
    this.collectConstraintInfos(logEntries);
    this.collectWarningsAndErrors(logEntries);
  }

  /**
   * Converts the collected log entries into a hierarchical structure.
   * @returns {*[]} Hierarchical log entries.
   */
  buildHierarchicalLogEntries() {
    const hierarchicalEntries = [];
    const modelGroups = groupBy(this.constraintEntries.values(), (e) => getModelName(e.constraintName));
    for (const modelName of Object.keys(modelGroups)) {
      const modelGroup = modelGroups[modelName];
      const modelEntry = {
        message: modelName,
        type: "Info",
        values: [],
      };
      hierarchicalEntries.push(modelEntry);

      const classGroups = groupBy(modelGroup, (e) => getClassNameOfConstraint(e.constraintName));
      for (const fullClassName of Object.keys(classGroups)) {
        const classGroup = classGroups[fullClassName];
        const className = fullClassName.substring(modelName.length + 1);
        const classEntry = {
          message: className,
          type: classGroup.reduce((type, e) => reduceType(type, e.type), "Info"),
          values: classGroup.map((e) => ({
            message: e.message.replaceAll(fullClassName + ".", ""),
            type: e.type,
          })),
        };
        modelEntry.type = reduceType(modelEntry.type, classEntry.type);
        modelEntry.values.push(classEntry);
      }
    }

    if (this.otherErrorMessages.size > 0 || this.otherWarningMessages.size > 0) {
      const errors = Array.from(this.otherErrorMessages).map((message) => ({ message, type: "Error" }));
      const warnings = Array.from(this.otherWarningMessages).map((message) => ({ message, type: "Warning" }));

      hierarchicalEntries.push({
        message: i18n.t("logHierarchy.otherMessages"),
        type: this.otherErrorMessages.size > 0 ? "Error" : "Warning",
        values: errors.concat(warnings),
      });
    }

    return hierarchicalEntries;
  }

  /**
   * Collects all constraints from the given logEntries without checking their validation results.
   * @private
   * @param {*[]} logEntries Entries of the validator log.
   */
  collectConstraintInfos(logEntries) {
    for (const logEntry of logEntries) {
      if (logEntry.type === "Info") {
        for (const [constraintType, pattern] of constraintPatterns) {
          const constraintMatch = pattern.exec(logEntry.message);
          if (constraintMatch) {
            const constraintName = constraintMatch[1];
            const message = constraintType + " " + constraintName;
            this.updateLogEntry(constraintName, logEntry.type, message);
            break;
          }
        }
      }
    }
  }

  /**
   * Collects warnings and errors of the constraints and other entries from the given logEntries.
   * @private
   * @param {*[]} logEntries Entries of the validator log.
   */
  collectWarningsAndErrors(logEntries) {
    for (const logEntry of logEntries) {
      if (logEntry.type !== "Info") {
        const customMessageMatch = customConstraintMessagePattern.exec(logEntry.message);
        if (customMessageMatch) {
          const customMessage = customMessageMatch[1];
          const constraintName = customMessageMatch[2];
          this.updateLogEntry(constraintName, logEntry.type, customMessage);
        } else {
          const nameMatch = constraintNamePattern.exec(logEntry.message);
          if (nameMatch) {
            const constraintName = nameMatch[1];
            const interlisSyntax = nameMatch[2];
            const logMessage = interlisSyntax ? logEntry.message.replace(interlisSyntax, "") : logEntry.message;
            this.updateLogEntry(constraintName, logEntry.type, logMessage);
          } else if (logEntry.type === "Error") {
            this.otherErrorMessages.add(logEntry.message);
          } else {
            this.otherWarningMessages.add(logEntry.message);
          }
        }
      }
    }
  }

  /**
   * Updates the log entry of the constraint with the given name.
   * @private
   * @param {string} constraintName The qualified name of the constraint.
   * @param {string} type The log entry type.
   * @param {string} message The log message.
   */
  updateLogEntry(constraintName, type, message) {
    this.constraintEntries.set(constraintName, {
      constraintName,
      type,
      message,
    });
  }
}

/**
 * Reduces the given types (Error, Warning or Info) to the type with higher priority.
 * @param {string} typeA The first type.
 * @param {string} typeB The second type.
 * @returns The type with higher priority.
 */
function reduceType(typeA, typeB) {
  if (typeA === "Error" || typeB === "Error") {
    return "Error";
  } else if (typeA === "Warning" || typeB === "Warning") {
    return "Warning";
  }
  return "Info";
}

/**
 * Gets the model name of the given qualified constraint name.
 * @param {string} qualifiedName The qualified name of the constraint.
 * @returns {string} The model name of the constraint.
 */
function getModelName(qualifiedName) {
  const index = qualifiedName.indexOf(".");
  return index === -1 ? qualifiedName : qualifiedName.substring(0, index);
}

/**
 * Gets the class name of the given qualified constraint name.
 * @param {string} qualifiedName The qualified name of the constraint.
 * @returns {string} The class name of the constraint.
 */
function getClassNameOfConstraint(qualifiedName) {
  const index = qualifiedName.lastIndexOf(".");
  return index === -1 ? qualifiedName : qualifiedName.substring(0, index);
}
