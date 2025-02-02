using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpMath.Editor {
  using Atoms;
  using Interfaces;
  public static class MathListEditingExtensions {
    static void InsertAtAtomIndexAndAdvance(this IMathList self, int atomIndex, IMathAtom atom, ref MathListIndex advance, MathListSubIndexType advanceType) {
      if (atomIndex < 0 || atomIndex > self.Count)
        throw new IndexOutOfRangeException($"Index {atomIndex} is out of bounds for list of size {self.Atoms.Count}");
      // Test for placeholder to the right of index, e.g. \sqrt{‸■} -> \sqrt{2‸}
      if (atomIndex < self.Count && self[atomIndex] is MathAtom placeholder &&
          placeholder?.AtomType is Enumerations.MathAtomType.Placeholder) {
        if (placeholder.Superscript is IMathList super) {
          if (atom.Superscript != null) super.Append(atom.Superscript);
          atom.Superscript = super;
        }
        if (placeholder.Subscript is IMathList sub) {
          if (atom.Subscript != null) sub.Append(atom.Subscript);
          atom.Subscript = sub;
        }
        self[atomIndex] = atom;
      } else self.Insert(atomIndex, atom);
      switch (advanceType) {
        case MathListSubIndexType.None:
          advance = advance.Next;
          break;
        default:
          advance = advance.LevelUpWithSubIndex(advanceType, MathListIndex.Level0Index(0));
          break;
      }
    }
    /// <summary>Inserts <paramref name="atom"/> and modifies <paramref name="index"/> to advance to the next position.</summary>
    public static void InsertAndAdvance(this IMathList self, ref MathListIndex index, IMathAtom atom, MathListSubIndexType advanceType) {
      index = index ?? MathListIndex.Level0Index(0);
      if (index.AtomIndex > self.Atoms.Count)
        throw new IndexOutOfRangeException($"Index {index.AtomIndex} is out of bounds for list of size {self.Atoms.Count}");
      switch (index.SubIndexType) {
        case MathListSubIndexType.None:
          self.InsertAtAtomIndexAndAdvance(index.AtomIndex, atom, ref index, advanceType);
          break;
        case MathListSubIndexType.BetweenBaseAndScripts:
          var currentAtom = self.Atoms[index.AtomIndex];
          if (currentAtom.Subscript == null && currentAtom.Superscript == null)
            throw new SubIndexTypeMismatchException("Nuclear fusion is not supported if there are neither subscripts nor superscripts in the current atom.");
          if (atom.Subscript != null || atom.Superscript != null)
            throw new ArgumentException("Cannot fuse with an atom that already has a subscript or a superscript");
          atom.Subscript = currentAtom.Subscript;
          atom.Superscript = currentAtom.Superscript;
          currentAtom.Subscript = null;
          currentAtom.Superscript = null;
          self.InsertAtAtomIndexAndAdvance(index.AtomIndex + index.SubIndex?.AtomIndex ?? 0, atom, ref index, advanceType);
          break;
        case MathListSubIndexType.Degree:
        case MathListSubIndexType.Radicand:
          if (!(self.Atoms[index.AtomIndex] is Radical radical && radical.AtomType == Enumerations.MathAtomType.Radical))
            throw new SubIndexTypeMismatchException($"No radical found at index {index.AtomIndex}");
          if (index.SubIndexType == MathListSubIndexType.Degree)
            radical.Degree.InsertAndAdvance(ref index.SubIndex, atom, advanceType);
          else
            radical.Radicand.InsertAndAdvance(ref index.SubIndex, atom, advanceType);
            break;
        case MathListSubIndexType.Numerator:
        case MathListSubIndexType.Denominator:
          if (!(self.Atoms[index.AtomIndex] is Fraction frac && frac.AtomType == Enumerations.MathAtomType.Fraction))
            throw new SubIndexTypeMismatchException($"No fraction found at index {index.AtomIndex}");
          if (index.SubIndexType == MathListSubIndexType.Numerator)
            frac.Numerator.InsertAndAdvance(ref index.SubIndex, atom, advanceType);
          else
            frac.Denominator.InsertAndAdvance(ref index.SubIndex, atom, advanceType);
            break;
        case MathListSubIndexType.Subscript:
          var current = self.Atoms[index.AtomIndex];
          if (current.Subscript == null) throw new SubIndexTypeMismatchException($"No subscript for atom at index {index.AtomIndex}");
          current.Subscript.InsertAndAdvance(ref index.SubIndex, atom, advanceType);
          break;
        case MathListSubIndexType.Superscript:
          current = self.Atoms[index.AtomIndex];
          if (current.Superscript == null) throw new SubIndexTypeMismatchException($"No superscript for atom at index {index.AtomIndex}");
          current.Superscript.InsertAndAdvance(ref index.SubIndex, atom, advanceType);
          break;
        default:
          throw new SubIndexTypeMismatchException("Invalid subindex type.");
      }
    }

    public static void RemoveAt(this IMathList self, MathListIndex index) {
      index = index ?? MathListIndex.Level0Index(0);
      if (index.AtomIndex > self.Atoms.Count)
        throw new IndexOutOfRangeException($"Index {index.AtomIndex} is out of bounds for list of size {self.Atoms.Count}");
      switch (index.SubIndexType) {
        case MathListSubIndexType.None:
          self.RemoveAt(index.AtomIndex);
          break;
        case MathListSubIndexType.BetweenBaseAndScripts:
          var currentAtom = self.Atoms[index.AtomIndex];
          if (currentAtom.Subscript == null && currentAtom.Superscript == null)
            throw new SubIndexTypeMismatchException("Nuclear fission is not supported if there are no subscripts or superscripts.");
          if (index.AtomIndex > 0) {
            var previous = self.Atoms[index.AtomIndex - 1];
            if (previous.Subscript is null && previous.Superscript is null) {
              previous.Superscript = currentAtom.Superscript;
              previous.Subscript = currentAtom.Subscript;
              self.RemoveAt(index.AtomIndex);
              break;
            }
          }
          // no previous atom or the previous atom sucks (has sub/super scripts)
          currentAtom.Nucleus = "";
          break;
        case MathListSubIndexType.Radicand:
        case MathListSubIndexType.Degree:
          if (!(self.Atoms[index.AtomIndex] is Radical radical && radical.AtomType == Enumerations.MathAtomType.Radical))
            throw new SubIndexTypeMismatchException($"No radical found at index {index.AtomIndex}");
          if (index.SubIndexType == MathListSubIndexType.Degree)
            radical.Degree.RemoveAt(index.SubIndex);
          else
            radical.Radicand.RemoveAt(index.SubIndex);
          break;
        case MathListSubIndexType.Numerator:
        case MathListSubIndexType.Denominator:
          if (!(self.Atoms[index.AtomIndex] is Fraction frac && frac.AtomType == Enumerations.MathAtomType.Fraction))
            throw new SubIndexTypeMismatchException($"No fraction found at index {index.AtomIndex}");
          if (index.SubIndexType == MathListSubIndexType.Numerator)
            frac.Numerator.RemoveAt(index.SubIndex);
          else
            frac.Denominator.RemoveAt(index.SubIndex);
          break;
        case MathListSubIndexType.Subscript:
          var current = self.Atoms[index.AtomIndex];
          if (current.Subscript == null) throw new SubIndexTypeMismatchException($"No subscript for atom at index {index.AtomIndex}");
          current.Subscript.RemoveAt(index.SubIndex);
          break;
        case MathListSubIndexType.Superscript:
          current = self.Atoms[index.AtomIndex];
          if (current.Superscript == null) throw new SubIndexTypeMismatchException($"No superscript for atom at index {index.AtomIndex}");
          current.Superscript.RemoveAt(index.SubIndex);
          break;
        default:
          throw new SubIndexTypeMismatchException("Invalid subindex type.");
      }
    }

    public static void RemoveAtoms(this IMathList self, MathListRange? nullableRange) {
      if (!(nullableRange is MathListRange range)) return;
      var start = range.Start;
      switch (start.SubIndexType) {
        case MathListSubIndexType.None:
          self.RemoveAtoms(new Range(start.AtomIndex, range.Length));
          break;
        case MathListSubIndexType.BetweenBaseAndScripts:
          throw new NotSupportedException("Nuclear fission is not supported");
        case MathListSubIndexType.Radicand:
        case MathListSubIndexType.Degree:
          if (!(self.Atoms[start.AtomIndex] is Radical radical && radical.AtomType == Enumerations.MathAtomType.Radical))
            throw new SubIndexTypeMismatchException($"No radical found at index {start.AtomIndex}");
          if (start.SubIndexType == MathListSubIndexType.Degree)
            radical.Degree.RemoveAtoms(range.SubIndexRange);
          else
            radical.Radicand.RemoveAtoms(range.SubIndexRange);
          break;
        case MathListSubIndexType.Numerator:
        case MathListSubIndexType.Denominator:
          if (!(self.Atoms[start.AtomIndex] is Fraction frac && frac.AtomType == Enumerations.MathAtomType.Fraction))
            throw new SubIndexTypeMismatchException($"No fraction found at index {start.AtomIndex}");
          if (start.SubIndexType == MathListSubIndexType.Numerator)
            frac.Numerator.RemoveAtoms(range.SubIndexRange);
          else
            frac.Denominator.RemoveAtoms(range.SubIndexRange);
          break;
        case MathListSubIndexType.Subscript:
          var current = self.Atoms[start.AtomIndex];
          if (current.Subscript == null) throw new SubIndexTypeMismatchException($"No subscript for atom at index {start.AtomIndex}");
          current.Subscript.RemoveAtoms(range.SubIndexRange);
          break;
        case MathListSubIndexType.Superscript:
          current = self.Atoms[start.AtomIndex];
          if (current.Superscript == null) throw new SubIndexTypeMismatchException($"No superscript for atom at index {start.AtomIndex}");
          current.Superscript.RemoveAtoms(range.SubIndexRange);
          break;
      }
    }
    
    [NullableReference]
    public static IMathAtom AtomAt(this IMathList self, MathListIndex index) {
      if (index is null || index.AtomIndex >= self.Atoms.Count) return null;
      var atom = self.Atoms[index.AtomIndex];
      switch (index.SubIndexType) {
        case MathListSubIndexType.None:
        case MathListSubIndexType.BetweenBaseAndScripts when index.AtomIndex == 1:
          return atom;
        case MathListSubIndexType.BetweenBaseAndScripts:
          return null;
        case MathListSubIndexType.Subscript:
          return atom.Subscript.AtomAt(index.SubIndex);
        case MathListSubIndexType.Superscript:
          return atom.Superscript.AtomAt(index.SubIndex);
        case MathListSubIndexType.Radicand:
        case MathListSubIndexType.Degree:
          return atom is Radical radical && radical.AtomType == Enumerations.MathAtomType.Radical ?
              index.SubIndexType == MathListSubIndexType.Degree ?
              radical.Degree.AtomAt(index.SubIndex) :
            radical.Radicand.AtomAt(index.SubIndex) :
            null;
        case MathListSubIndexType.Numerator:
        case MathListSubIndexType.Denominator:
          return atom is Fraction frac && frac.AtomType == Enumerations.MathAtomType.Fraction ?
            index.SubIndexType == MathListSubIndexType.Denominator ?
              frac.Denominator.AtomAt(index.SubIndex) :
              frac.Numerator.AtomAt(index.SubIndex) :
            null;
        default:
          throw new SubIndexTypeMismatchException("Invalid subindex type.");
      }
    }
  }
}
