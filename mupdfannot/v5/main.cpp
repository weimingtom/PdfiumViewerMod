#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <iostream>
#include <fstream>
#include <sstream>
#ifdef _WIN32
#include <windows.h>
#endif

#if 0
int main(int argc, char **argv)
{
	printf("hello world\n");
    getchar();
	return 0;
}
#endif

typedef struct PageInfo {
    int page = 0, n = 0, total = 0;
    int *counts = NULL;
    float *pts = NULL;
    int idx = 0;
    int idx2 = 0;    
} PageInfo;

static PageInfo *read_file(const char* name, int *pageCount);

#include <mupdf/fitz.h>
#include <mupdf/pdf.h>

#define MAX_SEARCH_HITS (500)
#define NUM_CACHE (3)
#define STRIKE_HEIGHT (0.375f)
#define UNDERLINE_HEIGHT (0.075f)
#define LINE_THICKNESS (0.07f)
#define INK_THICKNESS (2.0f)

#if 0
static void dump_annotation_display_lists(fz_context *ctx)
{
	int i;

	for (i = 0; i < NUM_CACHE; i++) {
		fz_drop_display_list(ctx, glo->pages[i].annot_list);
		glo->pages[i].annot_list = NULL;
	}
}
#endif

typedef struct rect_node_s rect_node;

struct rect_node_s
{
	fz_rect rect;
	rect_node *next;
};

typedef struct
{
	int number;
	int width;
	int height;
	fz_rect media_box;
	fz_rect rectTemp; //FIXME:新增
	fz_page *page;
	rect_node *changed_rects;
	rect_node *hq_changed_rects;
	fz_display_list *page_list;
	fz_display_list *annot_list;
} page_cache;

typedef struct globals_s globals;

struct globals_s
{
	int current;
	page_cache pages[NUM_CACHE];
};

struct globals_s glo_;
static int useGetChar = 1;

static char *tmp_path(const char *path)
{
	int f;
	char *buf = (char *)malloc(strlen(path) + 6 + 1);
	if (!buf)
		return NULL;

	strcpy(buf, path);
	strcat(buf, "XXXXXX");

#ifdef _WIN32
    char *result = _mktemp(buf);  
      if( result != NULL)  
      {
         FILE *fp = fopen(result, "w" );
         if(fp  == 0 )  
         {
             free(buf);
             return NULL;
         }
         else  
          {
                fclose( fp );  
                return result;
          }
      }
      else  
      {  
        free(buf);
		return NULL; 
      } 
#else
	f = mkstemp(buf);

	if (f >= 0)
	{
		close(f);
		return buf;
	}
	else
	{
		free(buf);
		return NULL;
	}
#endif
}

static void drop_changed_rects(fz_context *ctx, rect_node **nodePtr)
{
	rect_node *node = *nodePtr;
	while (node)
	{
		rect_node *tnode = node;
		node = node->next;
		fz_free(ctx, tnode);
	}

	*nodePtr = NULL;
}

static void drop_page_cache(fz_context *ctx, fz_document *doc, page_cache *pc)
{
	fprintf(stderr, "Drop page %d\n", pc->number);
	fz_drop_display_list(ctx, pc->page_list);
	pc->page_list = NULL;
	fz_drop_display_list(ctx, pc->annot_list);
	pc->annot_list = NULL;
	fz_drop_page(ctx, pc->page);
	pc->page = NULL;
	drop_changed_rects(ctx, &pc->changed_rects);
	drop_changed_rects(ctx, &pc->hq_changed_rects);
}

static void close_doc(fz_context *ctx, fz_document *doc)
{
     globals *glo = &glo_;   
	int i;

	for (i = 0; i < NUM_CACHE; i++)
		drop_page_cache(ctx, doc, &glo->pages[i]);
}

void save(fz_context *ctx, fz_document *doc, const char *current_path, const char *out_path)
{
	pdf_document *idoc = pdf_specifics(ctx, doc);

	if (idoc && current_path && out_path)
	{
		pdf_write_options opts = { 0 };

		opts.do_incremental = 1;

        fprintf(stderr, "out_path: %s\n", out_path);
        int written = 0;

        fz_var(written);
        fz_try(ctx)
        {
            FILE *fin = fopen(current_path, "rb");
            FILE *fout = fopen(out_path, "wb");
            char buf[256];
            int n, err = 1;

            if (fin && fout)
            {
                while ((n = fread(buf, 1, sizeof(buf), fin)) > 0)
                    fwrite(buf, 1, n, fout);
                err = (ferror(fin) || ferror(fout));
            }

            if (fin)
                fclose(fin);
            if (fout)
                fclose(fout);

            if (!err)
            {
                fprintf(stderr, "pdf_save_document begin: %s\n", out_path);
                pdf_save_document(ctx, idoc, out_path, &opts);
                written = 1;
            }
        }
        fz_catch(ctx)
        {
            written = 0;
        }

        if (written)
        {
            fprintf(stderr, "pdf_save_document written: %d\n", written);
            close_doc(ctx, doc);
        }
	}
}


void gotoPage(fz_context *ctx, fz_document *doc, int page, int resolution)
{
    globals *glo = &glo_;

	int i;
	int furthest;
	int furthest_dist = -1;
	float zoom;
	fz_matrix ctm;
	fz_irect bbox;
    page_cache *pc;
	
	for (i = 0; i < NUM_CACHE; i++)
	{
		if (glo->pages[i].page != NULL && glo->pages[i].number == page)
		{
			/* The page is already cached */
			glo->current = i;
			return;
		}

		if (glo->pages[i].page == NULL)
		{
			/* cache record unused, and so a good one to use */
			furthest = i;
			furthest_dist = INT_MAX;
		}
		else
		{
			int dist = abs(glo->pages[i].number - page);

			/* Further away - less likely to be needed again */
			if (dist > furthest_dist)
			{
				furthest_dist = dist;
				furthest = i;
			}
		}
	}

	glo->current = furthest;
	pc = &glo->pages[glo->current];

	drop_page_cache(ctx, doc, pc);

	/* In the event of an error, ensure we give a non-empty page */
	pc->width = 100;
	pc->height = 100;

	pc->number = page;
	LOGI("Goto page %d...", page);
	fz_try(ctx)
	{
		fz_rect rect;
		LOGI("Load page %d", pc->number);
		pc->page = fz_load_page(ctx, doc, pc->number);
		zoom = resolution / 72;
		fz_bound_page(ctx, pc->page, &pc->media_box);
		fz_scale(&ctm, zoom, zoom);
		rect = pc->media_box;
		LOGE("==================================MuPDFCore_gotoPageInternal MuPDFCore_getBoundsInternal %f, %f, %f, %f, %d, %d", rect.x0, rect.y0, rect.x1, rect.y1, pc->width, pc->height);
		pc->rectTemp = pc->media_box;
		fz_round_rect(&bbox, fz_transform_rect(&rect, &ctm));
		pc->width = bbox.x1-bbox.x0;
		pc->height = bbox.y1-bbox.y0;
	}
	fz_catch(ctx)
	{
		if (useGetChar) getchar();
		fprintf(stderr, "cannot make displaylist from page %d", pc->number);
	}
}

void addInkAnnotation(fz_context *ctx, fz_document *doc, int resolution, int nInk, int* countsInk, float* ptsInk)
{
    globals *glo = &glo_;    
    
	pdf_document *idoc = pdf_specifics(ctx, doc);
	page_cache *pc = &glo->pages[glo->current];
	int i, j, k, n;
	fz_point *pts = NULL;
	int *counts = NULL;
	int total = 0;
	float color[3];

	if (idoc == NULL)
		return;

	color[0] = 1.0;
	color[1] = 0.0;
	color[2] = 0.0;

	fz_var(pts);
	fz_var(counts);
	fz_try(ctx)
	{
		fz_annot *annot;
		fz_matrix ctm;

		float zoom = resolution / 72;
		zoom = 1.0 / zoom;
		fz_scale(&ctm, zoom, zoom);

		n = nInk; //FIXME:

		counts = (int *)fz_malloc_array(ctx, n, sizeof(int));

		for (i = 0; i < n; i++)
		{
			int count = countsInk[i]; //FIXME:

			counts[i] = count;
			total += count;
		}

		pts = (fz_point *)fz_malloc_array(ctx, total, sizeof(fz_point));

		k = 0;
		for (i = 0; i < n; i++)
		{
			int count = counts[i];

			for (j = 0; j < count; j++)
			{
				pts[k].x = ptsInk[k * 2]; //FIXME:
				pts[k].y = ptsInk[k * 2 + 1]; //FIXME:
				fz_transform_point(&pts[k], &ctm);
				k++;
			}
		}

		annot = (fz_annot *)pdf_create_annot(ctx, idoc, (pdf_page *)pc->page, FZ_ANNOT_INK);

		pdf_set_ink_annot_list(ctx, idoc, (pdf_annot *)annot, pts, counts, n, color, INK_THICKNESS);

		//dump_annotation_display_lists(glo);
	}
	fz_always(ctx)
	{
		fz_free(ctx, pts);
		fz_free(ctx, counts);
	}
	fz_catch(ctx)
	{
        if (useGetChar) getchar();
		fprintf(stderr, "addInkAnnotation: %s failed\n", ctx->error->message);
	}
    fprintf(stderr, "addInkAnnotation: success\n");
}


int main(int argc, char **argv)
{
	const char *input;
    const char *annot_filename;
	const char *output_filename;
    float zoom, rotate;
	int page_number, page_count;
	fz_context *ctx;
	fz_document *doc;
	//fz_pixmap *pix;
	fz_matrix ctm;
	int x, y;
    int pageCount = 0;
    
    useGetChar = argc > 1 ? atoi(argv[1]) : 1;
	input = argc > 2 && argv[2] != NULL ? argv[2] : "test.pdf";
    annot_filename = argc > 3 && argv[3] != NULL ? argv[3] : "annot.txt";
    output_filename = argc > 4 && argv[4] != NULL ? argv[4] : "test_out.pdf";
    
	page_number = 0;
	zoom = 100;
	rotate = 0;

    PageInfo * pageInfoArr = read_file(annot_filename, &pageCount);

	ctx = fz_new_context(NULL, NULL, FZ_STORE_UNLIMITED);
	if (!ctx)
	{
		fprintf(stderr, "cannot create mupdf context\n");
        if (useGetChar) getchar();
		return EXIT_FAILURE;
	}

	/* Register the default file types to handle. */
	fz_try(ctx)
		fz_register_document_handlers(ctx);
	fz_catch(ctx)
	{
		fprintf(stderr, "cannot register document handlers: %s\n", fz_caught_message(ctx));
		fz_drop_context(ctx);
        if (useGetChar) getchar();
		return EXIT_FAILURE;
	}

	/* Open the document. */
	fz_try(ctx)
		doc = fz_open_document(ctx, input);
	fz_catch(ctx)
	{
		fprintf(stderr, "cannot open document: %s\n", fz_caught_message(ctx));
		fz_drop_context(ctx);
        if (useGetChar) getchar();
		return EXIT_FAILURE;
	}

	/* Count the number of pages. */
	fz_try(ctx)
		page_count = fz_count_pages(ctx, doc);
	fz_catch(ctx)
	{
		fprintf(stderr, "cannot count number of pages: %s\n", fz_caught_message(ctx));
		fz_drop_document(ctx, doc);
		fz_drop_context(ctx);
        if (useGetChar) getchar();
		return EXIT_FAILURE;
	}

	if (page_number < 0 || page_number >= page_count)
	{
		fprintf(stderr, "page number out of range: %d (page count %d)\n", page_number + 1, page_count);
		fz_drop_document(ctx, doc);
		fz_drop_context(ctx);
        if (useGetChar) getchar();
		return EXIT_FAILURE;
	}
    fprintf(stderr, "pdf page count %d\n", page_count);
    
    for (int kkk = 0; kkk < pageCount; ++kkk) 
    {
        PageInfo *pageInfo = &pageInfoArr[kkk];        
        if (pageInfo->page >= 0 && pageInfo->page < page_count)
        {
            fprintf(stderr, ">>>>>>>>>gotoPage %d\n", pageInfo->page);
            gotoPage(ctx, doc, pageInfo->page, 72);
            //int n = 1;
            //int countsInk[] = {1};
            //float ptsInk[] = {10.0f, 10.0f};
            int n = pageInfo->n;
            int* countsInk = pageInfo->counts;
            float* ptsInk = pageInfo->pts;
            addInkAnnotation(ctx, doc, 72, n, countsInk, ptsInk);
        }
    }
    save(ctx, doc, input, output_filename);

	/* Clean up. */
	//fz_drop_pixmap(ctx, pix);
	fz_drop_document(ctx, doc);
	fz_drop_context(ctx);
    if (useGetChar) getchar();
	return EXIT_SUCCESS;
}



PageInfo *read_file(const char* name, int *pageCount) 
{
    using namespace std;
    
    char ch;
    ifstream file;
    file.open(name);
    if (file.fail()) 
    {
        cerr << "file open fail: " << name << endl;
        if (useGetChar) getchar();
        exit(0);
    }

    char* buf = new char[200];
    PageInfo * pageInfoArr = NULL;
    PageInfo * pageInfo = NULL;
    int pageIndex = 0;
    while (file.getline(buf, 200)) 
    {
        istringstream iss(buf);
        //fprintf(stderr, ">>> %s\n", buf);
        if (buf[0] == 'a')
        {
            iss >> ch >> *pageCount;
            pageInfoArr = (PageInfo *)malloc((*pageCount) * sizeof(PageInfo));
            cerr << "pageCount: " << (*pageCount) << endl;            
        }
        else if (buf[0] == 'g')
        {
            if (pageInfoArr != NULL)
            {
                pageInfo = &pageInfoArr[pageIndex++];
                pageInfo->page = pageInfo->n = pageInfo->total = 0;
                pageInfo->counts = NULL;
                pageInfo->pts = NULL;
                pageInfo->idx = pageInfo->idx2 = 0;

                iss >> ch >> pageInfo->page >> pageInfo->n >> pageInfo->total;
                pageInfo->counts = (int*)calloc(pageInfo->n, sizeof(int));
                pageInfo->pts = (float*)calloc(pageInfo->total * 2, sizeof(float));
                cerr << "page: " << pageInfo->page << " n: " << pageInfo->n << " total: " << pageInfo->total << endl;
            }
        }
        else if (buf[0] == 'c') 
        {
            int c;
            iss >> ch >> c;
            if (pageInfo != NULL && pageInfo->counts != NULL)
            {
                pageInfo->counts[pageInfo->idx++] = c;
                cerr << "counts: " << c << endl;
            }
        }
        else if (buf[0] == 'p') 
        {
            float x, y;
            iss >> ch >> x >> y;
            if (pageInfo != NULL && pageInfo->pts != NULL)
            {
                pageInfo->pts[pageInfo->idx2++] = x;
                pageInfo->pts[pageInfo->idx2++] = y;
                //cerr << "pts: x:" << x << " y:" << y << endl;
            }
        }
    }
    cerr << "============ read_file end" << endl; 
    return pageInfoArr;
}
