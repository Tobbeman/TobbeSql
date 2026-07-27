# Learnings and reflections

Claude is a great guide, until it is not.
Most of the lessons and code was straightforward until we started doing the index.
The intital design claude suggested ( Changing the root whenever we split ) meant we had to update the json on each insert, in case the root changed.
Futher more, the code was not really allowing us to easily fetch root page etc.

Claude also started writing more of the code, writing the entire BTreeNode itself
rather than letting me.

At the time I was OK with this, as I was growing "bored" writing all the code; I just
wanted it to work!

That is in itself a reflection. As I kept going, I was less and less interested in writing the code, but rather just wanted it to work. While fine, that meant I thought less about it, which is kind of counterproductive when I do this to learn.
As a example, I asked claude several times for help during the BTree. All the other lessons I did myself.

If I keep working on this project, here is some stuff we might want to do:
- Clean up code. Got messy towards the end.
- Implement index support for strings
- Implement index support for several keys
- Implement primary key support
- Implement count() operation
- Implement min/max operation