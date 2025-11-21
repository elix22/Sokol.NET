# GitHub Pages Deployment Guide

This guide will help you deploy the Sokol.NET examples showcase to GitHub Pages.

## Prerequisites

- Repository must be public (or GitHub Pro/Team for private repos)
- You must have admin access to the repository

## Step 1: Build All WebAssembly Examples

Run the build script to compile all examples and copy them to the `docs/` folder:

```bash
./scripts/build-all-web-examples.sh
```

This will:
- Build all 36 examples for WebAssembly
- Copy the compiled output to `docs/examples/`
- Take approximately 15-30 minutes depending on your machine

**Note**: The build script will skip any examples that fail to build, so check the output for any errors.

## Step 2: Add `.nojekyll` File

GitHub Pages uses Jekyll by default, which can interfere with files starting with `_`. Create a `.nojekyll` file to disable Jekyll:

```bash
touch docs/.nojekyll
```

This file is already created in this guide.

## Step 3: Commit and Push Changes

```bash
git add docs/
git commit -m "Add WebAssembly examples showcase for GitHub Pages"
git push origin main
```

## Step 4: Enable GitHub Pages

1. Go to your repository on GitHub: https://github.com/elix22/Sokol.NET

2. Click **Settings** (top right)

3. Scroll down to **Pages** (in the left sidebar under "Code and automation")

4. Under **Source**, select:
   - **Branch**: `main`
   - **Folder**: `/docs`

5. Click **Save**

6. GitHub will display the URL where your site will be published:
   ```
   https://elix22.github.io/Sokol.NET/
   ```

## Step 5: Wait for Deployment

- Deployment typically takes 1-5 minutes
- You'll see a green checkmark when it's ready
- Click the "Visit site" button to view your showcase

## Step 6: Update README

Add a prominent link to the live showcase in your README.md:

```markdown
## 🌐 Live Examples

**[Try all 36 examples in your browser →](https://elix22.github.io/Sokol.NET/)**

Experience Sokol.NET's capabilities instantly with interactive WebAssembly examples. No installation required!
```

## Troubleshooting

### Examples Not Loading

1. **Check browser console** for errors
2. **Verify CORS headers** - GitHub Pages serves with correct headers by default
3. **Check file paths** - ensure all files are in `docs/examples/{example-name}/`

### 404 Errors

1. **Verify the docs folder structure**:
   ```
   docs/
   ├── index.html
   ├── examples-data.json
   ├── css/
   │   └── showcase.css
   ├── js/
   │   └── showcase.js
   ├── examples/
   │   ├── cube/
   │   │   ├── index.html
   │   │   ├── *.wasm
   │   │   └── *.js
   │   └── ... (other examples)
   └── thumbnails/
       └── .gitkeep
   ```

2. **Check .nojekyll file exists** in the docs folder

### Large Repository Size

If your repository becomes too large (GitHub has a 1GB soft limit):

1. **Use Git LFS** for WASM files:
   ```bash
   git lfs track "docs/examples/**/*.wasm"
   git lfs track "docs/examples/**/*.dat"
   ```

2. **Consider hosting WASM files elsewhere** (CDN, GitHub Releases)

3. **Exclude some examples** from the web build if they're too large

## Updating Examples

When you update examples:

1. Run the build script again
2. Commit and push changes
3. GitHub Pages will automatically rebuild (1-5 minutes)

## Custom Domain (Optional)

To use a custom domain like `examples.sokolnet.com`:

1. Add a `CNAME` file to `docs/`:
   ```bash
   echo "examples.sokolnet.com" > docs/CNAME
   ```

2. Configure DNS:
   - Add a CNAME record pointing to `elix22.github.io`

3. Enable HTTPS in GitHub Pages settings (automatic after DNS propagates)

## Performance Tips

1. **Enable caching** - GitHub Pages sets appropriate cache headers
2. **Compress WASM files** - .NET already does this during build
3. **Use thumbnails** - Add real screenshots to `docs/thumbnails/` for faster page load

## Analytics (Optional)

To track usage, add Google Analytics or similar to `docs/index.html`:

```html
<!-- Add before </head> -->
<script async src="https://www.googletagmanager.com/gtag/js?id=GA_MEASUREMENT_ID"></script>
<script>
  window.dataLayer = window.dataLayer || [];
  function gtag(){dataLayer.push(arguments);}
  gtag('js', new Date());
  gtag('config', 'GA_MEASUREMENT_ID');
</script>
```

## Security

GitHub Pages is secure by default:
- ✅ HTTPS enabled automatically
- ✅ DDoS protection
- ✅ Content served through GitHub's CDN
- ✅ No server-side code (static files only)

## Support

If you encounter issues:

1. Check [GitHub Pages documentation](https://docs.github.com/en/pages)
2. View [GitHub Status](https://www.githubstatus.com/) for outages
3. Open an issue in the Sokol.NET repository
