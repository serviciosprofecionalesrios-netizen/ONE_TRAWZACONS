(() => {
    const category = document.getElementById('Category');
    const element = document.getElementById('Element');
    const catalog = JSON.parse(document.getElementById('averias-elements').textContent);
    function update(selected) {
        element.replaceChildren(new Option('Selecciona el elemento de daño', ''));
        for (const value of catalog[category.value] || []) element.add(new Option(value, value, false, value === selected));
        element.disabled = !category.value;
    }
    category.addEventListener('change', () => update(''));
    update(element.dataset.selected || '');
})();
