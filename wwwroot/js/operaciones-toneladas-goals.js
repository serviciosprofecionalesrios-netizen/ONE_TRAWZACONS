(function (root) {
    function monthGoal(date, goals, fallback) {
        return goals.find(g => g.year === date.getFullYear() && g.month === date.getMonth() + 1) || fallback;
    }
    function rangeGoal(from, to, goals, fallback) {
        let total = 0;
        let cursor = new Date(from.getFullYear(), from.getMonth(), from.getDate());
        const end = new Date(to.getFullYear(), to.getMonth(), to.getDate());
        while (cursor <= end) {
            const monthEnd = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0);
            const stop = monthEnd < end ? monthEnd : end;
            const goal = monthGoal(cursor, goals, fallback);
            const fullMonth = cursor.getDate() === 1 && stop.getDate() === monthEnd.getDate();
            total += fullMonth ? Number(goal.monthly || 0) : Number(goal.daily || 0) * (stop.getDate() - cursor.getDate() + 1);
            cursor = new Date(stop.getFullYear(), stop.getMonth(), stop.getDate() + 1);
        }
        return total;
    }
    root.ToneladasGoals = { monthGoal, rangeGoal };
})(globalThis);
