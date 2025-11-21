// Sokol.NET Examples Showcase

let examplesData = null;
let currentCategory = 'all';

// Load examples data and initialize
async function init() {
    try {
        const response = await fetch('examples-data.json');
        examplesData = await response.json();
        
        renderCategoryFilters();
        renderExamples();
    } catch (error) {
        console.error('Failed to load examples data:', error);
        document.getElementById('examplesGrid').innerHTML = 
            '<p class="loading">Failed to load examples. Please refresh the page.</p>';
    }
}

// Render category filter buttons
function renderCategoryFilters() {
    const filtersContainer = document.getElementById('categoryFilters');
    
    // Add "All" button (already in HTML)
    // Add category buttons
    examplesData.categories.forEach(category => {
        const button = document.createElement('button');
        button.className = 'filter-btn';
        button.dataset.category = category;
        button.textContent = category;
        button.onclick = () => filterByCategory(category);
        filtersContainer.appendChild(button);
    });
}

// Filter examples by category
function filterByCategory(category) {
    currentCategory = category;
    
    // Update active button
    document.querySelectorAll('.filter-btn').forEach(btn => {
        btn.classList.remove('active');
        if (btn.dataset.category === category || 
            (category === 'all' && btn.dataset.category === 'all')) {
            btn.classList.add('active');
        }
    });
    
    renderExamples();
}

// Render example cards
function renderExamples() {
    const grid = document.getElementById('examplesGrid');
    grid.innerHTML = '';
    
    const examples = currentCategory === 'all' 
        ? examplesData.examples 
        : examplesData.examples.filter(ex => ex.category === currentCategory);
    
    if (examples.length === 0) {
        grid.innerHTML = '<p class="loading">No examples found in this category.</p>';
        return;
    }
    
    examples.forEach(example => {
        const card = createExampleCard(example);
        grid.appendChild(card);
    });
}

// Create an example card element
function createExampleCard(example) {
    const card = document.createElement('div');
    card.className = 'example-card';
    card.onclick = () => openExample(example);
    
    // Check if thumbnail exists, otherwise use gradient with emoji
    const thumbnailContent = example.thumbnail && example.thumbnail !== 'thumbnails/placeholder.png'
        ? `<img src="${example.thumbnail}" alt="${example.name}" onerror="this.parentElement.innerHTML='🎮'">`
        : '🎮';
    
    card.innerHTML = `
        <div class="example-thumbnail">
            ${thumbnailContent}
        </div>
        <div class="example-info">
            <div class="example-category">${example.category}</div>
            <h3 class="example-name">${example.name}</h3>
            <p class="example-description">${example.description}</p>
        </div>
    `;
    
    return card;
}

// Open example in modal
function openExample(example) {
    const modal = document.getElementById('exampleModal');
    const title = document.getElementById('modalTitle');
    const frame = document.getElementById('exampleFrame');
    const sourceLink = document.getElementById('sourceLink');
    
    title.textContent = example.name;
    sourceLink.href = example.source;
    
    // Load the example's index.html in the iframe
    frame.src = `examples/${example.id}/index.html`;
    
    modal.classList.add('active');
    
    // Prevent body scroll when modal is open
    document.body.style.overflow = 'hidden';
}

// Close modal
function closeModal() {
    const modal = document.getElementById('exampleModal');
    const frame = document.getElementById('exampleFrame');
    
    modal.classList.remove('active');
    frame.src = ''; // Stop the example
    
    // Restore body scroll
    document.body.style.overflow = '';
}

// Event listeners
document.getElementById('closeModal').onclick = closeModal;

// Close modal when clicking outside
document.getElementById('exampleModal').onclick = (e) => {
    if (e.target.id === 'exampleModal') {
        closeModal();
    }
};

// Close modal with Escape key
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeModal();
    }
});

// Filter "All Examples" button
document.querySelector('[data-category="all"]').onclick = () => filterByCategory('all');

// Initialize on page load
document.addEventListener('DOMContentLoaded', init);
